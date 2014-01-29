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

    static class PolygonHelper
    {
        public static int size(this Polygon polygon)
        {
            return polygon.Count;
        }

#if false
        public static Point this[int index]
        {
            get { return polygon[index]; }
        }
#endif

        public static void add(this Polygon p0, Point p)
        {
            polygon.Add(p);
        }

        public static void remove(this Polygon polygon, int index)
        {
            throw new NotImplementedException();
            //polygon.erase(polygon.begin() + index);
        }

        public static void clear(this Polygon polygon)
        {
            polygon.Clear();
        }

        public static bool orientation(this Polygon polygon)
        {
            throw new NotImplementedException();
            //return ClipperLib.Orientation(polygon);
        }

        public static void reverse(this Polygon polygon)
        {
            throw new NotImplementedException();
            //ClipperLib.ReversePolygons(polygon);
        }

        public static long polygonLength(this Polygon polygon)
        {
            long length = 0;
            Point p0 = polygon[polygon.Count - 1];
            for (int n = 0; n < polygon.Count; n++)
            {
                Point p1 = polygon[n];
                length += vSize(p0 - p1);
                p0 = p1;
            }
            return length;
        }

        public static double area(this Polygon polygon)
        {
            return ClipperLib.Area(polygon);
        }

        public static Point centerOfMass(this Polygon polygon)
        {
            double x = 0, y = 0;
            Point p0 = (polygon)[polygon.Count - 1];
            for (int n = 0; n < polygon.Count; n++)
            {
                Point p1 = (polygon)[n];
                double second_factor = (p0.X * p1.Y) - (p1.X * p0.Y);

                x += (double)(p0.X + p1.X) * second_factor;
                y += (double)(p0.Y + p1.Y) * second_factor;
                p0 = p1;
            }

            double area = Area(polygon);
            x = x / 6 / area;
            y = y / 6 / area;

            if (x < 0)
            {
                x = -x;
                y = -y;
            }
            return new Point(x, y);
        }
    }
#if false

public class _Polygon : PolygonRef
{
    Path poly;

public _Polygon()
    : PolygonRef(poly)
    {
    }
}

//#define Polygon _Polygon
#endif

    static class PolygonsHelper
    {
    public static int size(this Polygons polygons)
    {
        return polygons.Count;
    }

#if false
            public static PolygonRef this[int index]
    {
        return new PolygonRef(polygons[index]);
    }
#endif
    public static void remove(this Polygons polygons, int index)
    {
        polygons.RemoveAt(index);
    }
    public static void clear(this Polygons polygons)
    {
        polygons.Clear();
    }

    public static void add(this Polygons polygons, PolygonRef poly)
    {
        polygons.Add(poly.polygon);
    }

    public static void add(this Polygons polygons, Polygons other)
    {
        for(int n=0; n<other.polygons.Count; n++)
            polygons.Add(other.polygons[n]);
    }

    public static PolygonRef newPoly(this Polygons polygons)
    {
        polygons.Add(new ClipperLib.Path());
        return PolygonRef(polygons[polygons.Count-1]);
    }
    
#if false
    public static Polygons operator=( Polygons other) 
    {
        polygons = other.polygons; 
        return *this; 
    }
#endif

    public static Polygons difference(this Polygons polygons, Polygons other) 
    {
        Polygons ret;
        ClipperLib.Clipper clipper;
        clipper.AddPolygons(polygons, ClipperLib.PolyType.ptSubject, true);
        clipper.AddPaths(other.polygons, ClipperLib.ptClip, true);
        clipper.Execute(ClipperLib.ctDifference, ret.polygons);
        return ret;
    }

    public static Polygons unionPolygons(this Polygons polygons, Polygons other) 
    {
        Polygons ret;
        ClipperLib.Clipper clipper;
        clipper.AddPaths(polygons, ClipperLib.PolyType.ptSubject, true);
        clipper.AddPaths(other.polygons, ClipperLib.PolyType.ptSubject, true);
        clipper.Execute(ClipType.ctUnion, ret.polygons, ClipperLib.pftNonZero, ClipperLib.pftNonZero);
        return ret;
    }

    public static Polygons intersection(this Polygons polygons, Polygons other) 
    {
        Polygons ret;
        ClipperLib.Clipper clipper;
        clipper.AddPaths(polygons, ClipperLib.PolyType.ptSubject, true);
        clipper.AddPaths(other.polygons, ClipperLib.ptClip, true);
        clipper.Execute(ClipperLib.ctIntersection, ret.polygons);
        return ret;
    }

    public static Polygons offset(this Polygons polygons, int distance) 
    {
        Polygons ret;
        ClipperLib.ClipperOffset clipper;
        clipper.AddPaths(polygons, ClipperLib.JoinType.jtMiter, ClipperLib.EndType.etClosedPolygon);
        clipper.MiterLimit = 2.0;
        clipper.Execute(ret.polygons, distance);
        return ret;
    }
    public static List<Polygons> splitIntoParts(this Polygons polygons, bool unionAll = false) 
    {
        List<Polygons> ret;
        ClipperLib.Clipper clipper;
        ClipperLib.PolyTree resultPolyTree;
        clipper.AddPaths(polygons, ClipperLib.PolyType.ptSubject, true);
        if (unionAll)
            clipper.Execute(ClipType.ctUnion, resultPolyTree, ClipperLib.PolyFillType.pftNonZero, ClipperLib.PolyFillType.pftNonZero);
        else
            clipper.Execute(ClipType.ctUnion, resultPolyTree);
        
        polygons._processPolyTreeNode(resultPolyTree, ret);
        return ret;
    }

    static void _processPolyTreeNode(this Polygons polygonsIn, PolyNode node, List<Polygons> ret) 
    {
        for(int n=0; n<node.ChildCount; n++)
        {
            ClipperLib.PolyNode child = node.Childs[n];
            Polygons polygons = new Polygons();
            polygons.add(child.Contour);
            for(int i=0; i<child.ChildCount; i++)
            {
                polygons.add(child.Childs[i].Contour);
                polygonsIn._processPolyTreeNode(child.Childs[i], ret);
            }
            ret.Add(polygons);
        }
    }

    public static Polygons processEvenOdd(this Polygons polygons) 
    {
        Polygons ret;
        ClipperLib.Clipper clipper;
        clipper.AddPaths(polygons, ClipperLib.PolyType.ptSubject, true);
        clipper.Execute(ClipType.ctUnion, ret.polygons);
        return ret;
    }
    
    public static long polygonLength(this Polygons polygons)
    {
        long length = 0;
        for(int i=0; i<polygons.Count; i++)
        {
            Point p0 = polygons[i][polygons[i].Count-1];
            for(int n=0; n<polygons[i].Count; n++)
            {
                Point p1 = polygons[i][n];
                length += (p0 - p1).vSize();
                p0 = p1;
            }
        }
        return length;
    }

    static void applyMatrix(this Polygons polygons, PointMatrix matrix)
    {
        for(int i=0; i<polygons.Count; i++)
        {
            for(int j=0; j<polygons[i].Count; j++)
            {
                polygons[i][j] = matrix.apply(polygons[i][j]);
            }
        }
    }
}

    /* Axis aligned boundary box */
    public class AABB
    {
        public Point min, max;

        public AABB()
        {
            min = new Point(long.MinValue, long.MinValue);
            max = new Point(long.MinValue, long.MinValue);
        }

        public AABB(Polygons polys)
        {
            min = new Point(long.MinValue, long.MinValue);
            max = new Point(long.MinValue, long.MinValue);
            calculate(polys);
        }

        public void calculate(Polygons polys)
        {
            min = new Point(long.MaxValue, long.MaxValue);
            max = new Point(long.MinValue, long.MinValue);
            for (int i = 0; i < polys.Count; i++)
            {
                for (int j = 0; j < polys[i].Count; j++)
                {
                    if (min.X > polys[i][j].X) min.X = polys[i][j].X;
                    if (min.Y > polys[i][j].Y) min.Y = polys[i][j].Y;
                    if (max.X < polys[i][j].X) max.X = polys[i][j].X;
                    if (max.Y < polys[i][j].Y) max.Y = polys[i][j].Y;
                }
            }
        }

        public bool hit(AABB other)
        {
            if (max.X < other.min.X) return false;
            if (min.X > other.max.X) return false;
            if (max.Y < other.min.Y) return false;
            if (min.Y > other.max.Y) return false;
            return true;
        }
    }
}