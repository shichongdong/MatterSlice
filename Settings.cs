﻿/*
Copyright (C) 2013 David Braam
Copyright (c) 2014, Lars Brubaker

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
using System.Linq;
using System.Text;
using System.Reflection;

using ClipperLib;

namespace MatterHackers.MatterSlice
{
    public class _ConfigSettingIndex
    {
        public string key;
        int ptr;

        _ConfigSettingIndex(string key, int ptr)
        {
            this.key = key;
            this.ptr = ptr;
            throw new NotImplementedException();
        }
    }

    // this class is so that we can change the name of a variable and not break old settings files
    [System.AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = true)]
    public class LegacyName : System.Attribute
    {
        private string name;
        public string Name { get { return name; } }

        public LegacyName(string name)
        {
            this.name = name;
        }
    }

    // all the variables in this class will be saved and loaded from settings files
    public class ConfigSettings
    {
        // if you were to change the layerThickness variable you would add a legacy name so that we can still use old settings
        //[LegacyName("exampleLegacyLayerThickness")]
        public double layerThickness;
        public int layerThickness_µm { get { return (int)(layerThickness * 1000); } }

        public double initialLayerThicknessMm;
        public int initialLayerThickness_µm { get { return (int)(initialLayerThicknessMm * 1000); } }

        public int filamentDiameter;
        public int filamentFlow;
        public int firstLayerExtrusionWidth;
        public int extrusionWidth;
        public int insetCount;
        public int downSkinCount;
        public int upSkinCount;
        public int sparseInfillLineDistance;
        public int infillOverlap;
        public int infillAngleDegrees;
        public int skirtDistance;
        public int skirtLineCount;
        public int skirtMinLength;
        public int retractionAmount;
        public int retractionAmountExtruderSwitch;
        public int retractionSpeed;
        public int retractionMinimalDistance;
        public int minimalExtrusionBeforeRetraction;
        public int retractionZHop;
        public bool enableCombing;
        public bool enableOozeShield;
        public int wipeTowerSize;
        public int multiVolumeOverlap;

        // speed settings
        public int initialSpeedupLayers;
        public int initialLayerSpeed;
        public int printSpeed;
        public int infillSpeed;
        public int inset0Speed;
        public int insetXSpeed;
        public int moveSpeed;
        public int fanFullOnLayerNr;

        //Support material
        public ConfigConstants.SUPPORT_TYPE supportType;
        public int supportAngleDegrees;
        public int supportEverywhere;
        public int supportLineDistance;
        public int supportXYDistance;
        public int supportZDistance;
        public int supportExtruder;

        //Cool settings
        public int minimalLayerTime;
        public int minimalFeedrate;
        public bool coolHeadLift;
        public int fanSpeedMinPercent;
        public int fanSpeedMaxPercent;

        //Raft settings
        public int raftMargin;
        public int raftLineSpacing;
        public int raftBaseThickness;
        public int raftBaseLinewidth;
        public int raftInterfaceThickness;
        public int raftInterfaceLinewidth;

        public FMatrix3x3 matrix = new FMatrix3x3();
        public IntPoint objectPosition;
        public int objectSink;

        public ConfigConstants.FIX_HORRIBLE fixHorrible;
        public bool spiralizeMode;
        public ConfigConstants.GCODE_FLAVOR gcodeFlavor;

        public IntPoint[] extruderOffset = new IntPoint[ConfigConstants.MAX_EXTRUDERS];
        public string startCode;
        public string endCode;

        public ConfigSettings()
        {
            SetToDefault();
        }

        public void SetToDefault()
        {
            filamentDiameter = 2890;
            filamentFlow = 100;
            initialLayerThicknessMm = .3;
            layerThickness = .1;
            firstLayerExtrusionWidth = 800;
            extrusionWidth = 400;
            insetCount = 2;
            downSkinCount = 6;
            upSkinCount = 6;
            initialSpeedupLayers = 4;
            initialLayerSpeed = 20;
            printSpeed = 50;
            infillSpeed = 50;
            inset0Speed = 50;
            insetXSpeed = 50;
            moveSpeed = 200;
            fanFullOnLayerNr = 2;
            skirtDistance = 6000;
            skirtLineCount = 1;
            skirtMinLength = 0;
            sparseInfillLineDistance = 100 * extrusionWidth / 20;
            infillOverlap = 15;
            infillAngleDegrees = 45;
            objectPosition.X = 102500;
            objectPosition.Y = 102500;
            objectSink = 0;
            supportType = ConfigConstants.SUPPORT_TYPE.GRID;
            supportAngleDegrees = -1;
            supportEverywhere = 0;
            supportLineDistance = sparseInfillLineDistance;
            supportExtruder = -1;
            supportXYDistance = 700;
            supportZDistance = 150;
            retractionAmount = 4500;
            retractionSpeed = 45;
            retractionAmountExtruderSwitch = 14500;
            retractionMinimalDistance = 1500;
            minimalExtrusionBeforeRetraction = 100;
            enableOozeShield = false;
            enableCombing = true;
            wipeTowerSize = 0;
            multiVolumeOverlap = 0;

            minimalLayerTime = 5;
            minimalFeedrate = 10;
            coolHeadLift = false;
            fanSpeedMinPercent = 100;
            fanSpeedMaxPercent = 100;

            raftMargin = 5000;
            raftLineSpacing = 1000;
            raftBaseThickness = 0;
            raftBaseLinewidth = 0;
            raftInterfaceThickness = 0;
            raftInterfaceLinewidth = 0;

            spiralizeMode = false;
            fixHorrible = 0;
            gcodeFlavor = ConfigConstants.GCODE_FLAVOR.REPRAP;

            startCode =
                            "M109 S210     ;Heatup to 210C\n" +
                            "G21           ;metric values\n" +
                            "G90           ;absolute positioning\n" +
                            "G28           ;Home\n" +
                            "G1 Z15.0 F300 ;move the platform down 15mm\n" +
                            "G92 E0        ;zero the extruded length\n" +
                            "G1 F200 E5    ;extrude 5mm of feed stock\n" +
                            "G92 E0        ;zero the extruded length again\n";
            endCode =
                "M104 S0                     ;extruder heater off\n" +
                "M140 S0                     ;heated bed heater off (if you have it)\n" +
                "G91                            ;relative positioning\n" +
                "G1 E-1 F300                    ;retract the filament a bit before lifting the nozzle, to release some of the pressure\n" +
                "G1 Z+0.5 E-5 X-20 Y-20 F9000   ;move Z up a bit and retract filament even more\n" +
                "G28 X0 Y0                      ;move X/Y to min endstops, so the head is out of the way\n" +
                "M84                         ;steppers off\n" +
                "G90                         ;absolute positioning\n";
        }

        public void DumpSettings(string fileName)
        {
            List<string> lines = new List<string>();
            FieldInfo[] fields;
            fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                System.Attribute[] attributes = System.Attribute.GetCustomAttributes(field);
                foreach (Attribute attribute in attributes)
                {
                    LegacyName legacyName = attribute as LegacyName;
                    if (legacyName != null)
                    {
                        string Name = legacyName.Name;
                    }
                }
                string name = field.Name;
                object value = field.GetValue(this);
                switch (field.FieldType.Name)
                {
                    case "Int32":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "Double":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "Boolean":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "FMatrix3x3":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "IntPoint":
                        lines.Add("{0}={1}".FormatWith(name, ((IntPoint)value).OutputInMm()));
                        break;

                    case "IntPoint[]":
                        lines.Add("{0}={1}".FormatWith(name, value));
                        break;

                    case "String":
                        lines.Add("{0}={1}".FormatWith(name, value).Replace("\n", "\\n"));
                        break;

                    case "FIX_HORRIBLE":
                        {
                            lines.Add("{0}={1} # {2}".FormatWith(name, value, GetEnumHelpText(field.FieldType, field.FieldType.Name)));
                        }
                        break;

                    case "SUPPORT_TYPE":
                            lines.Add("{0}={1} # {2}".FormatWith(name, value, GetEnumHelpText(field.FieldType, field.FieldType.Name)));
                        break;

                    case "GCODE_FLAVOR":
                            lines.Add("{0}={1} # {2}".FormatWith(name, value, GetEnumHelpText(field.FieldType, field.FieldType.Name)));
                        break;

                    default:
                        throw new NotImplementedException("unknown type");
                }
            }

            lines.Sort();
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName))
            {
                foreach (string line in lines)
                {
                    file.WriteLine(line);
                }
            }
        }

        private static string GetEnumHelpText(Type type, string enumName)
        {
            bool first = true;
            string helpLine = "Available Values: ";
            FieldInfo[] fields = type.GetFields();
            foreach (FieldInfo field in fields)
            {
                string[] names = field.ToString().Split(' ');
                if (names.Length == 2 && names[0] == enumName)
                {
                    if (!first)
                    {
                        helpLine += ", ";
                    }
                    helpLine += names[1];
                    first = false;
                }
            }

            return helpLine;
        }

        public bool SetSetting(string keyToSet, string valueToSetTo)
        {
            valueToSetTo = valueToSetTo.Replace("\"", "").Trim();

            List<string> lines = new List<string>();
            FieldInfo[] fields;
            fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                System.Attribute[] attributes = System.Attribute.GetCustomAttributes(field);
                List<string> possibleNames = new List<string>();
                possibleNames.Add(field.Name);
                foreach (Attribute attribute in attributes)
                {
                    LegacyName legacyName = attribute as LegacyName;
                    if (legacyName != null)
                    {
                        possibleNames.Add(legacyName.Name);
                    }
                }

                if (possibleNames.Contains(keyToSet))
                {
                    string name = field.Name;
                    object value = field.GetValue(this);
                    switch (field.FieldType.Name)
                    {
                        case "Int32":
                            field.SetValue(this, (int)double.Parse(valueToSetTo));
                            break;

                        case "Double":
                            field.SetValue(this, double.Parse(valueToSetTo));
                            break;

                        case "Boolean":
                            field.SetValue(this, bool.Parse(valueToSetTo));
                            break;

                        case "FMatrix3x3":
                            throw new NotImplementedException();
                            break;

                        case "IntPoint":
                            throw new NotImplementedException();
                            break;

                        case "IntPoint[]":
                            throw new NotImplementedException();
                            break;

                        case "String":
                            field.SetValue(this, valueToSetTo);
                            break;

                        case "FIX_HORRIBLE":
                            throw new NotImplementedException();
                            break;

                        case "SUPPORT_TYPE":
                            throw new NotImplementedException();
                            break;

                        case "GCODE_FLAVOR":
                            throw new NotImplementedException();
                            break;

                        default:
                            throw new NotImplementedException("unknown type");
                    }

                    return true;
                }
            }
            return false;
        }

        public bool ReadSettings(string fileName)
        {
            throw new NotImplementedException();
        }
    }

    public class ConfigConstants
    {
        public const string VERSION = "1.0";

        [Flags]
        public enum FIX_HORRIBLE
        {
            NONE,
            UNION_ALL_TYPE_A = 0x01,
            UNION_ALL_TYPE_B = 0x02,
            EXTENSIVE_STITCHING = 0x04,
            UNION_ALL_TYPE_C = 0x08,
            KEEP_NONE_CLOSED = 0x10,
        }

        /**
         * * Type of support material.
         * * Grid is a X/Y grid with an outline, which is very strong, provides good support. But in some cases is hard to remove.
         * * Lines give a row of lines which break off one at a time, making them easier to remove, but they do not support as good as the grid support.
         * */
        public enum SUPPORT_TYPE
        {
            GRID,
            LINES
        }

        public enum GCODE_FLAVOR
        {
            /**
             * RepRap flavored GCode is Marlin/Sprinter/Repetier based GCode. 
             *  This is the most commonly used GCode set.
             *  G0 for moves, G1 for extrusion.
             *  E values give mm of filament extrusion.
             *  Retraction is done on E values with G1. Start/end code is added.
             *  M106 Sxxx and M107 are used to turn the fan on/off.
             **/
            REPRAP,
            /**
             * UltiGCode flavored is Marlin based GCode. 
             *  UltiGCode uses less settings on the slicer and puts more settings in the firmware. This makes for more hardware/material independed GCode.
             *  G0 for moves, G1 for extrusion.
             *  E values give mm^3 of filament extrusion. Ignores the filament diameter setting.
             *  Retraction is done with G10 and G11. Retraction settings are ignored. G10 S1 is used for multi-extruder switch retraction.
             *  Start/end code is not added.
             *  M106 Sxxx and M107 are used to turn the fan on/off.
             **/
            ULTIGCODE,
            /**
             * Makerbot flavored GCode.
             *  Looks a lot like RepRap GCode with a few changes. Requires MakerWare to convert to X3G files.
             *   Heating needs to be done with M104 Sxxx T0
             *   No G21 or G90
             *   Fan ON is M126 T0 (No fan strength control?)
             *   Fan OFF is M127 T0
             *   Homing is done with G162 X Y F2000
             **/
            MAKERBOT,

            /**
             * Bits From Bytes GCode.
             *  BFB machines use RPM instead of E. Which is coupled to the F instead of independed. (M108 S[deciRPM])
             *  Need X,Y,Z,F on every line.
             *  Needs extruder ON/OFF (M101, M103), has auto-retrection (M227 S[2560*mm] P[2560*mm])
             **/
            BFB,

            /**
              * MACH3 GCode
              *  MACH3 is CNC control software, which expects A/B/C/D for extruders, instead of E.
              **/
            MACH3,
        }


        public const int MAX_EXTRUDERS = 16;
    }
}

