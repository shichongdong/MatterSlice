/*
Copyright (c) 2013, Lars Brubaker

This file is part of MatterSlice.

MatterSlice is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

MatterSlice is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with MatterSlice.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    using Point = IntPoint;
    using Polygon = List<IntPoint>;
    using Polygons = List<Polygon>;
    using PolygonRef = Polygon;

    //FusedFilamentFabrication processor.
    public class fffProcessor
    {
        int maxObjectHeight;
        int fileNr;
        GCodeExport gcode;
        ConfigSettings config;
        Stopwatch timeKeeper;

        GCodePathConfig skirtConfig;
        GCodePathConfig inset0Config;
        GCodePathConfig inset1Config;
        GCodePathConfig fillConfig;
        GCodePathConfig supportConfig;

        static void log(string output)
        {
            Console.Write(output);
        }

        public fffProcessor(ConfigSettings config)
        {
            this.config = config;
            fileNr = 1;
            maxObjectHeight = 0;
        }

        public bool setTargetFile(string filename)
        {
            gcode.setFilename(filename);
            if (gcode.isValid())
                gcode.addComment(string.Format("Generated with MatterSlice {0}", ConfigSettings.VERSION));
            return gcode.isValid();
        }

        public bool processFile(string input_filename)
        {
            if (!gcode.isValid())
                return false;

            Stopwatch timeKeeperTotal;
            SliceDataStorage storage;
            preSetup();
            if (!prepareModel(storage, input_filename))
                return false;

            processSliceData(storage);
            writeGCode(storage);

            logProgress("process", 1, 1);
            log("Total time elapsed %5.2fs.\n", timeKeeperTotal.restart());

            return true;
        }

        public void finalize()
        {
            if (!gcode.isValid())
                return;

            gcode.addFanCommand(0);
            gcode.addRetraction();
            gcode.setZ(maxObjectHeight + 5000);
            gcode.addMove(gcode.getPositionXY(), config.moveSpeed, 0);
            gcode.addCode(config.endCode);
            log(string.Format("Print time: {0}\n", (int)(gcode.getTotalPrintTime()))));
            log(string.Format("Filament: {0}\n", (int)(gcode.getTotalFilamentUsed(0))));
            log(string.Format("Filament2: {0}\n", (int)(gcode.getTotalFilamentUsed(1))));

            if (gcode.getFlavor() == ConfigSettings.GCODE_FLAVOR_ULTIGCODE)
            {
                char[] numberString = new char[16];
                sprintf(numberString, "%d", (int)(gcode.getTotalPrintTime()));
                gcode.replaceTagInStart("<__TIME__>", numberString);
                sprintf(numberString, "%d", (int)(gcode.getTotalFilamentUsed(0)));
                gcode.replaceTagInStart("<FILAMENT>", numberString);
                sprintf(numberString, "%d", (int)(gcode.getTotalFilamentUsed(1)));
                gcode.replaceTagInStart("<FILAMEN2>", numberString);
            }
        }

        void preSetup()
        {
            skirtConfig.setData(config.printSpeed, config.extrusionWidth, "SKIRT");
            inset0Config.setData(config.printSpeed, config.extrusionWidth, "WALL-OUTER");
            inset1Config.setData(config.printSpeed, config.extrusionWidth, "WALL-INNER");
            fillConfig.setData(config.infillSpeed, config.extrusionWidth, "FILL");
            supportConfig.setData(config.printSpeed, config.extrusionWidth, "SUPPORT");

            for (int n = 1; n < ConfigSettings.MAX_EXTRUDERS; n++)
                gcode.setExtruderOffset(n, config.extruderOffset[n].p());
            gcode.setFlavor(config.gcodeFlavor);
            gcode.setRetractionSettings(config.retractionAmount, config.retractionSpeed, config.retractionAmountExtruderSwitch, config.minimalExtrusionBeforeRetraction);
        }

        bool prepareModel(SliceDataStorage storage, string input_filename)
        {
            timeKeeper.restart();
            log("Loading %s from disk...\n", input_filename);
            SimpleModel m = loadModel(input_filename, config.matrix);
            if (!m)
            {
                logError("Failed to load model: %s\n", input_filename);
                return false;
            }
            log("Loaded from disk in %5.3fs\n", timeKeeper.Restart());
            log("Analyzing and optimizing model...\n");
            OptimizedModel om = new OptimizedModel(m, new Point3(config.objectPosition.X, config.objectPosition.Y, -config.objectSink));
            for (int v = 0; v < m.volumes.Count; v++)
            {
                log("  Face counts: %i . %i %0.1f%%\n", (int)m.volumes[v].faces.Count, (int)om.volumes[v].faces.Count, (float)(om.volumes[v].faces.Count) / (float)(m.volumes[v].faces.Count) * 100);
                log("  Vertex counts: %i . %i %0.1f%%\n", (int)m.volumes[v].faces.Count * 3, (int)om.volumes[v].points.Count, (float)(om.volumes[v].points.Count) / (float)(m.volumes[v].faces.Count * 3) * 100);
            }

#if !DEBUG
            m = null;
#endif

            log("Optimize model %5.3fs \n", timeKeeper.restart());
            //om.saveDebugSTL("c:\\models\\output.stl");

            log("Slicing model...\n");
            List<Slicer> slicerList;
            for (int volumeIdx = 0; volumeIdx < om.volumes.Count; volumeIdx++)
            {
                Slicer slicer = new Slicer(&om.volumes[volumeIdx], config.initialLayerThickness - config.layerThickness / 2, config.layerThickness, config.fixHorrible & ConfigSettings.FIX_HORRIBLE_KEEP_NONE_CLOSED, config.fixHorrible & ConfigSettings.FIX_HORRIBLE_EXTENSIVE_STITCHING);
                slicerList.push_back(slicer);
                for (int layerNr = 0; layerNr < slicer.layers.Count; layerNr++)
                {
                    //Reporting the outline here slows down the engine quite a bit, so only do so when debugging.
                    //logPolygons("outline", layerNr, slicer.layers[layerNr].z, slicer.layers[layerNr].polygonList);
                    logPolygons("openoutline", layerNr, slicer.layers[layerNr].z, slicer.layers[layerNr].openPolygonList);
                }
            }
            log("Sliced model in %5.3fs\n", timeKeeper.restart());

            log("Generating support map...\n");
            generateSupportGrid(storage.support, om, config.supportAngle, config.supportEverywhere > 0, config.supportXYDistance, config.supportZDistance);

            storage.modelSize = om.modelSize;
            storage.modelMin = om.vMin;
            storage.modelMax = om.vMax;
#if !DEBUG
        om = null;
#endif

            log("Generating layer parts...\n");
            for (int volumeIdx = 0; volumeIdx < slicerList.Count; volumeIdx++)
            {
                storage.volumes.push_back(SliceVolumeStorage());
                createLayerParts(storage.volumes[volumeIdx], slicerList[volumeIdx], config.fixHorrible & (ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_A | ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_B | ConfigSettings.FIX_HORRIBLE_UNION_ALL_TYPE_C));
                slicerList[volumeIdx] = null;
            }
            log("Generated layer parts in %5.3fs\n", timeKeeper.restart());
            return true;
        }

        void processSliceData(SliceDataStorage storage)
        {
            //carveMultipleVolumes(storage.volumes);
            generateMultipleVolumesOverlap(storage.volumes, config.multiVolumeOverlap);
            //dumpLayerparts(storage, "c:/models/output.html");

            int totalLayers = storage.volumes[0].layers.Count;
            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                {
                    int insetCount = config.insetCount;
                    if (config.spiralizeMode && (int)(layerNr) < config.downSkinCount && layerNr % 2 == 1)//Add extra insets every 2 layers when spiralizing, this makes bottoms of cups watertight.
                        insetCount += 5;
                    SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
                    generateInsets(layer, config.extrusionWidth, insetCount);

                    for (int partNr = 0; partNr < layer.parts.Count; partNr++)
                    {
                        if (layer.parts[partNr].insets.Count > 0)
                        {
                            logPolygons("inset0", layerNr, layer.z, layer.parts[partNr].insets[0]);
                            for (int inset = 1; inset < layer.parts[partNr].insets.Count; inset++)
                                logPolygons("insetx", layerNr, layer.z, layer.parts[partNr].insets[inset]);
                        }
                    }
                }
                logProgress("inset", layerNr + 1, totalLayers);
            }
            if (config.enableOozeShield)
            {
                for (int layerNr = 0; layerNr < totalLayers; layerNr++)
                {
                    Polygons oozeShield;
                    for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                    {
                        for (int partNr = 0; partNr < storage.volumes[volumeIdx].layers[layerNr].parts.Count; partNr++)
                        {
                            oozeShield = oozeShield.unionPolygons(storage.volumes[volumeIdx].layers[layerNr].parts[partNr].outline.offset(2000));
                        }
                    }
                    storage.oozeShield.push_back(oozeShield);
                }

                for (int layerNr = 0; layerNr < totalLayers; layerNr++)
                    storage.oozeShield[layerNr] = storage.oozeShield[layerNr].offset(-1000).offset(1000);
                int offsetAngle = Math.Tan(60.0 * Math.PI / 180) * config.layerThickness;//Allow for a 60deg angle in the oozeShield.
                for (int layerNr = 1; layerNr < totalLayers; layerNr++)
                    storage.oozeShield[layerNr] = storage.oozeShield[layerNr].unionPolygons(storage.oozeShield[layerNr - 1].offset(-offsetAngle));
                for (int layerNr = totalLayers - 1; layerNr > 0; layerNr--)
                    storage.oozeShield[layerNr - 1] = storage.oozeShield[layerNr - 1].unionPolygons(storage.oozeShield[layerNr].offset(-offsetAngle));
            }
            log("Generated inset in %5.3fs\n", timeKeeper.Restart());

            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                if (!config.spiralizeMode || (int)(layerNr) < config.downSkinCount)    //Only generate up/downskin and infill for the first X layers when spiralize is choosen.
                {
                    for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
                    {
                        generateSkins(layerNr, storage.volumes[volumeIdx], config.extrusionWidth, config.downSkinCount, config.upSkinCount, config.infillOverlap);
                        generateSparse(layerNr, storage.volumes[volumeIdx], config.extrusionWidth, config.downSkinCount, config.upSkinCount);

                        SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
                        for (int partNr = 0; partNr < layer.parts.Count; partNr++)
                            logPolygons("skin", layerNr, layer.z, layer.parts[partNr].skinOutline);
                    }
                }
                logProgress("skin", layerNr + 1, totalLayers);
            }
            log("Generated up/down skin in %5.3fs\n", timeKeeper.restart());

            if (config.wipeTowerSize > 0)
            {
                PolygonRef p = storage.wipeTower.newPoly();
                p.add(new Point(storage.modelMin.x - 3000, storage.modelMax.y + 3000));
                p.add(new Point(storage.modelMin.x - 3000, storage.modelMax.y + 3000 + config.wipeTowerSize));
                p.add(new Point(storage.modelMin.x - 3000 - config.wipeTowerSize, storage.modelMax.y + 3000 + config.wipeTowerSize));
                p.add(new Point(storage.modelMin.x - 3000 - config.wipeTowerSize, storage.modelMax.y + 3000));

                storage.wipePoint = new Point(storage.modelMin.x - 3000 - config.wipeTowerSize / 2, storage.modelMax.y + 3000 + config.wipeTowerSize / 2);
            }

            generateSkirt(storage, config.skirtDistance, config.extrusionWidth, config.skirtLineCount, config.skirtMinLength, config.initialLayerThickness);
            generateRaft(storage, config.raftMargin);

            for (int volumeIdx = 0; volumeIdx < storage.volumes.Count; volumeIdx++)
            {
                for (int layerNr = 0; layerNr < totalLayers; layerNr++)
                {
                    for (int partNr = 0; partNr < storage.volumes[volumeIdx].layers[layerNr].parts.Count; partNr++)
                    {
                        if (layerNr > 0)
                            storage.volumes[volumeIdx].layers[layerNr].parts[partNr].bridgeAngle = bridgeAngle(storage.volumes[volumeIdx].layers[layerNr].parts[partNr], storage.volumes[volumeIdx].layers[layerNr - 1]);
                        else
                            storage.volumes[volumeIdx].layers[layerNr].parts[partNr].bridgeAngle = -1;
                    }
                }
            }
        }

        void writeGCode(SliceDataStorage storage)
        {
            if (fileNr == 1)
            {
                if (gcode.getFlavor() == ConfigSettings.GCODE_FLAVOR_ULTIGCODE)
                {
                    gcode.addCode(";FLAVOR:UltiGCode");
                    gcode.addCode(";TIME:<__TIME__>");
                    gcode.addCode(";MATERIAL:<FILAMENT>");
                    gcode.addCode(";MATERIAL2:<FILAMEN2>");
                }
                gcode.addCode(config.startCode);
            }
            else
            {
                gcode.addFanCommand(0);
                gcode.resetExtrusionValue();
                gcode.addRetraction();
                gcode.setZ(maxObjectHeight + 5000);
                gcode.addMove(Point(storage.modelMin.x, storage.modelMin.y), config.moveSpeed, 0);
            }
            fileNr++;

            int totalLayers = storage.volumes[0].layers.Count;
            gcode.addComment("Layer count: %d", totalLayers);

            if (config.raftBaseThickness > 0 && config.raftInterfaceThickness > 0)
            {
                GCodePathConfig raftBaseConfig = new GCodePathConfig(config.initialLayerSpeed, config.raftBaseLinewidth, "SUPPORT");
                GCodePathConfig raftInterfaceConfig = new GCodePathConfig(config.initialLayerSpeed, config.raftInterfaceLinewidth, "SUPPORT");
                {
                    gcode.addComment("LAYER:-2");
                    gcode.addComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.moveSpeed, config.retractionMinimalDistance);
                    gcode.setZ(config.raftBaseThickness);
                    gcode.setExtrusion(config.raftBaseThickness, config.filamentDiameter, config.filamentFlow);
                    gcodeLayer.addPolygonsByOptimizer(storage.raftOutline, &raftBaseConfig);

                    Polygons raftLines;
                    generateLineInfill(storage.raftOutline, raftLines, config.raftBaseLinewidth, config.raftLineSpacing, config.infillOverlap, 0);
                    gcodeLayer.addPolygonsByOptimizer(raftLines, &raftBaseConfig);

                    gcodeLayer.writeGCode(false, config.raftBaseThickness);
                }

                {
                    gcode.addComment("LAYER:-1");
                    gcode.addComment("RAFT");
                    GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.moveSpeed, config.retractionMinimalDistance);
                    gcode.setZ(config.raftBaseThickness + config.raftInterfaceThickness);
                    gcode.setExtrusion(config.raftInterfaceThickness, config.filamentDiameter, config.filamentFlow);

                    Polygons raftLines;
                    generateLineInfill(storage.raftOutline, raftLines, config.raftInterfaceLinewidth, config.raftLineSpacing, config.infillOverlap, 90);
                    gcodeLayer.addPolygonsByOptimizer(raftLines, &raftInterfaceConfig);

                    gcodeLayer.writeGCode(false, config.raftInterfaceThickness);
                }
            }

            int volumeIdx = 0;
            for (int layerNr = 0; layerNr < totalLayers; layerNr++)
            {
                logProgress("export", layerNr + 1, totalLayers);

                gcode.addComment("LAYER:%d", layerNr);
                if (layerNr == 0)
                    gcode.setExtrusion(config.initialLayerThickness, config.filamentDiameter, config.filamentFlow);
                else
                    gcode.setExtrusion(config.layerThickness, config.filamentDiameter, config.filamentFlow);

                GCodePlanner gcodeLayer = new GCodePlanner(gcode, config.moveSpeed, config.retractionMinimalDistance);
                int z = config.initialLayerThickness + layerNr * config.layerThickness;
                z += config.raftBaseThickness + config.raftInterfaceThickness;
                gcode.setZ(z);

                bool printSupportFirst = (storage.support.generated && config.supportExtruder > 0 && config.supportExtruder == gcodeLayer.getExtruder());
                if (printSupportFirst)
                    addSupportToGCode(storage, gcodeLayer, layerNr);

                for (int volumeCnt = 0; volumeCnt < storage.volumes.Count; volumeCnt++)
                {
                    if (volumeCnt > 0)
                        volumeIdx = (volumeIdx + 1) % storage.volumes.Count;
                    addVolumeLayerToGCode(storage, gcodeLayer, volumeIdx, layerNr);
                }
                if (!printSupportFirst)
                    addSupportToGCode(storage, gcodeLayer, layerNr);

                //Finish the layer by applying speed corrections for minimal layer times and slowdown for the initial layer.
                if ((int)(layerNr) < config.initialSpeedupLayers)
                {
                    int n = config.initialSpeedupLayers;
                    int layer0Factor = config.initialLayerSpeed * 100 / config.printSpeed;
                    gcodeLayer.setExtrudeSpeedFactor((layer0Factor * (n - layerNr) + 100 * (layerNr)) / n);
                    if (layerNr == 0)//On the first layer, also slow down the travel
                        gcodeLayer.setTravelSpeedFactor(layer0Factor);
                }
                gcodeLayer.forceMinimalLayerTime(config.minimalLayerTime, config.minimalFeedrate);

                int fanSpeed = config.fanSpeedMin;
                if (gcodeLayer.getExtrudeSpeedFactor() <= 50)
                {
                    fanSpeed = config.fanSpeedMax;
                }
                else
                {
                    int n = gcodeLayer.getExtrudeSpeedFactor() - 50;
                    fanSpeed = config.fanSpeedMin * n / 50 + config.fanSpeedMax * (50 - n) / 50;
                }
                if ((int)(layerNr) < config.fanFullOnLayerNr)
                {
                    //Slow down the fan on the layers below the [fanFullOnLayerNr], where layer 0 is speed 0.
                    fanSpeed = fanSpeed * layerNr / config.fanFullOnLayerNr;
                }
                gcode.addFanCommand(fanSpeed);

                gcodeLayer.writeGCode(config.coolHeadLift > 0, (int)(layerNr) > 0 ? config.layerThickness : config.initialLayerThickness);
            }

            /* support debug
            for(int y=0; y<storage.support.gridHeight; y++)
            {
                for(int x=0; x<storage.support.gridWidth; x++)
                {
                    int n = x+y*storage.support.gridWidth;
                    if (storage.support.grid[n].Count < 1) continue;
                    int z = storage.support.grid[n][0].z;
                    gcode.addMove(Point3(x * storage.support.gridScale + storage.support.gridOffset.X, y * storage.support.gridScale + storage.support.gridOffset.Y, 0), 0);
                    gcode.addMove(Point3(x * storage.support.gridScale + storage.support.gridOffset.X, y * storage.support.gridScale + storage.support.gridOffset.Y, z), z);
                    gcode.addMove(Point3(x * storage.support.gridScale + storage.support.gridOffset.X, y * storage.support.gridScale + storage.support.gridOffset.Y, 0), 0);
                }
            }
            //*/

            log("Wrote layers in %5.2fs.\n", timeKeeper.restart());
            gcode.tellFileSize();
            gcode.addFanCommand(0);

            //Store the object height for when we are printing multiple objects, as we need to clear every one of them when moving to the next position.
            maxObjectHeight = Math.Max(maxObjectHeight, storage.modelSize.z);
        }

        //Add a single layer from a single mesh-volume to the GCode
        void addVolumeLayerToGCode(SliceDataStorage storage, GCodePlanner gcodeLayer, int volumeIdx, int layerNr)
        {
            int prevExtruder = gcodeLayer.getExtruder();
            bool extruderChanged = gcodeLayer.setExtruder(volumeIdx);
            if (layerNr == 0 && volumeIdx == 0)
                gcodeLayer.addPolygonsByOptimizer(storage.skirt, &skirtConfig);

            SliceLayer layer = storage.volumes[volumeIdx].layers[layerNr];
            if (extruderChanged)
                addWipeTower(storage, gcodeLayer, layerNr, prevExtruder);

            if (storage.oozeShield.Count > 0 && storage.volumes.Count > 1)
            {
                gcodeLayer.setAlwaysRetract(true);
                gcodeLayer.addPolygonsByOptimizer(storage.oozeShield[layerNr], skirtConfig);
                logPolygons("oozeshield", layerNr, layer.z, storage.oozeShield[layerNr]);
                gcodeLayer.setAlwaysRetract(!config.enableCombing);
            }

            PathOrderOptimizer partOrderOptimizer = new PathOrderOptimizer(gcode.getPositionXY());
            for (int partNr = 0; partNr < layer.parts.Count; partNr++)
            {
                partOrderOptimizer.addPolygon(layer.parts[partNr].insets[0][0]);
            }
            partOrderOptimizer.optimize();

            for (int partCounter = 0; partCounter < partOrderOptimizer.polyOrder.Count; partCounter++)
            {
                SliceLayerPart part = layer.parts[partOrderOptimizer.polyOrder[partCounter]];

                if (config.enableCombing)
                    gcodeLayer.setCombBoundary(part.combBoundery);
                else
                    gcodeLayer.setAlwaysRetract(true);

                if (config.insetCount > 0)
                {
                    if (config.spiralizeMode)
                    {
                        if ((int)(layerNr) >= config.downSkinCount)
                            inset0Config.spiralize = true;
                        if ((int)(layerNr) == config.downSkinCount && part.insets.Count > 0)
                            gcodeLayer.addPolygonsByOptimizer(part.insets[0], inset1Config);
                    }
                    for (int insetNr = part.insets.Count - 1; insetNr > -1; insetNr--)
                    {
                        if (insetNr == 0)
                            gcodeLayer.addPolygonsByOptimizer(part.insets[insetNr], inset0Config);
                        else
                            gcodeLayer.addPolygonsByOptimizer(part.insets[insetNr], inset1Config);
                    }
                }

                Polygons fillPolygons;
                int fillAngle = 45;
                if (layerNr & 1)
                    fillAngle += 90;
                //int sparseSteps[1] = {config.extrusionWidth};
                //generateConcentricInfill(part.skinOutline, fillPolygons, sparseSteps, 1);
                generateLineInfill(part.skinOutline, fillPolygons, config.extrusionWidth, config.extrusionWidth, config.infillOverlap, (part.bridgeAngle > -1) ? part.bridgeAngle : fillAngle);
                //int sparseSteps[2] = {config.extrusionWidth*5, config.extrusionWidth * 0.8};
                //generateConcentricInfill(part.sparseOutline, fillPolygons, sparseSteps, 2);
                if (config.sparseInfillLineDistance > 0)
                {
                    if (config.sparseInfillLineDistance > config.extrusionWidth * 4)
                    {
                        generateLineInfill(part.sparseOutline, fillPolygons, config.extrusionWidth, config.sparseInfillLineDistance * 2, config.infillOverlap, 45);
                        generateLineInfill(part.sparseOutline, fillPolygons, config.extrusionWidth, config.sparseInfillLineDistance * 2, config.infillOverlap, 45 + 90);
                    }
                    else
                    {
                        generateLineInfill(part.sparseOutline, fillPolygons, config.extrusionWidth, config.sparseInfillLineDistance, config.infillOverlap, fillAngle);
                    }
                }

                gcodeLayer.addPolygonsByOptimizer(fillPolygons, fillConfig);
                logPolygons("infill", layerNr, layer.z, fillPolygons);

                //After a layer part, make sure the nozzle is inside the comb boundary, so we do not retract on the perimeter.
                if (!config.spiralizeMode || (int)(layerNr) < config.downSkinCount)
                    gcodeLayer.moveInsideCombBoundary(config.extrusionWidth * 2);
            }
            gcodeLayer.setCombBoundary(null);
        }

        void addSupportToGCode(SliceDataStorage storage, GCodePlanner gcodeLayer, int layerNr)
        {
            if (!storage.support.generated)
                return;

            if (config.supportExtruder > -1)
            {
                int prevExtruder = gcodeLayer.getExtruder();
                if (gcodeLayer.setExtruder(config.supportExtruder))
                    addWipeTower(storage, gcodeLayer, layerNr, prevExtruder);

                if (storage.oozeShield.Count > 0 && storage.volumes.Count == 1)
                {
                    gcodeLayer.setAlwaysRetract(true);
                    gcodeLayer.addPolygonsByOptimizer(storage.oozeShield[layerNr], skirtConfig);
                    gcodeLayer.setAlwaysRetract(!config.enableCombing);
                }
            }
            int z = config.initialLayerThickness + layerNr * config.layerThickness;
            SupportPolyGenerator supportGenerator = new SupportPolyGenerator(storage.support, z);
            for (int volumeCnt = 0; volumeCnt < storage.volumes.Count; volumeCnt++)
            {
                SliceLayer layer = storage.volumes[volumeCnt].layers[layerNr];
                for (int n = 0; n < layer.parts.Count; n++)
                    supportGenerator.polygons = supportGenerator.polygons.difference(layer.parts[n].outline.offset(config.supportXYDistance));
            }
            //Contract and expand the suppory polygons so small sections are removed and the final polygon is smoothed a bit.
            supportGenerator.polygons = supportGenerator.polygons.offset(-config.extrusionWidth * 3);
            supportGenerator.polygons = supportGenerator.polygons.offset(config.extrusionWidth * 3);
            logPolygons("support", layerNr, z, supportGenerator.polygons);

            List<Polygons> supportIslands = supportGenerator.polygons.splitIntoParts();

            for (int n = 0; n < supportIslands.Count; n++)
            {
                Polygons supportLines;
                if (config.supportLineDistance > 0)
                {
                    if (config.supportLineDistance > config.extrusionWidth * 4)
                    {
                        generateLineInfill(supportIslands[n], supportLines, config.extrusionWidth, config.supportLineDistance * 2, config.infillOverlap, 0);
                        generateLineInfill(supportIslands[n], supportLines, config.extrusionWidth, config.supportLineDistance * 2, config.infillOverlap, 90);
                    }
                    else
                    {
                        generateLineInfill(supportIslands[n], supportLines, config.extrusionWidth, config.supportLineDistance, config.infillOverlap, (layerNr & 1) ? 0 : 90);
                    }
                }

                gcodeLayer.forceRetract();
                if (config.enableCombing)
                    gcodeLayer.setCombBoundary(supportIslands[n]);
                gcodeLayer.addPolygonsByOptimizer(supportIslands[n], supportConfig);
                gcodeLayer.addPolygonsByOptimizer(supportLines, supportConfig);
                gcodeLayer.setCombBoundary(null);
            }
        }

        void addWipeTower(SliceDataStorage storage, GCodePlanner gcodeLayer, int layerNr, int prevExtruder)
        {
            if (config.wipeTowerSize < 1)
                return;
            //If we changed extruder, print the wipe/prime tower for this nozzle;
            gcodeLayer.addPolygonsByOptimizer(storage.wipeTower, supportConfig);
            Polygons fillPolygons;
            generateLineInfill(storage.wipeTower, fillPolygons, config.extrusionWidth, config.extrusionWidth, config.infillOverlap, 45 + 90 * (layerNr % 2));
            gcodeLayer.addPolygonsByOptimizer(fillPolygons, supportConfig);

            //Make sure we wipe the old extruder on the wipe tower.
            gcodeLayer.addTravel(storage.wipePoint - config.extruderOffset[prevExtruder].p() + config.extruderOffset[gcodeLayer.getExtruder()].p());
        }
    }
}