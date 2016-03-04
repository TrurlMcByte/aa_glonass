// 
// Based on classes of Eric Marchesin - <eric.marchesin@laposte.net> 2003
//-----------------------------------------------------------------------
using System;
using Jungler.Bot.Classes;

namespace Dwader.Navigation {
    /// <summary>
    /// Basic geometry class : easy to replace
    /// Written so as to be generalized
    /// </summary>
    [Serializable]
    public class Point3D {
        double[] _Coords = new double[3];

        /// <summary>
        /// Point3D constructor.
        /// </summary>
        /// <exception cref="ArgumentNullException">Argument array must not be null.</exception>
        /// <exception cref="ArgumentException">The Coordinates' array must contain exactly 3 elements.</exception>
        /// <param name="Coordinates">An array containing the three coordinates' values.</param>
        public Point3D(double[] Coordinates) {
            if (Coordinates == null) throw new ArgumentNullException();
            if (Coordinates.Length != 3) throw new ArgumentException("The Coordinates' array must contain exactly 3 elements.");
            X = Coordinates[0]; Y = Coordinates[1]; Z = Coordinates[2];
        }

        /// <summary>
        /// Point3D constructor.
        /// </summary>
        /// <param name="CoordinateX">X coordinate.</param>
        /// <param name="CoordinateY">Y coordinate.</param>
        /// <param name="CoordinateZ">Z coordinate.</param>
        public Point3D(double CoordinateX, double CoordinateY, double CoordinateZ) {
            X = CoordinateX; Y = CoordinateY; Z = CoordinateZ;
        }

        /// <summary>
        /// Accede to coordinates by indexes.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Index must belong to [0;2].</exception>
        public double this[int CoordinateIndex] {
            get { return _Coords[CoordinateIndex]; }
            set { _Coords[CoordinateIndex] = value; }
        }

        /// <summary>
        /// Gets/Set X coordinate.
        /// </summary>
        public double X { set { _Coords[0] = value; } get { return _Coords[0]; } }

        /// <summary>
        /// Gets/Set Y coordinate.
        /// </summary>
        public double Y { set { _Coords[1] = value; } get { return _Coords[1]; } }

        /// <summary>
        /// Gets/Set Z coordinate.
        /// </summary>
        public double Z { set { _Coords[2] = value; } get { return _Coords[2]; } }

        /// <summary>
        /// optional name
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// internal type
        /// </summary>
        public int type { get; set; }
        /// <summary>
        /// radius of point (usually modelRadius)
        /// </summary>
        public double radius { get; set; }
        /// <summary>
        /// dist to next ???
        /// </summary>
        public double ndist = 0;
        /// <summary>
        /// Constructor overload with name
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="name"></param>
        public Point3D(double x, double y, double z, string name) {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.name = name;
            this.type = 1;
            this.radius = 1;
        }
        /// <summary>
        /// type cast
        /// </summary>
        /// <param name="creature">creature</param>
        /// <returns></returns>
        public static implicit operator Point3D(Creature creature) { return new Point3D(creature); }
        /// <summary>
        /// type cast
        /// </summary>
        /// <param name="self">me</param>
        /// <returns></returns>
        public static implicit operator Point3D(Self self) { return new Point3D(self); }
        /// <summary>
        /// type cast
        /// </summary>
        /// <param name="p">GpsPoint</param>
        /// <returns></returns>
        public static implicit operator Point3D(GpsPoint p) { return new Point3D(p); }
        
        //public static implicit operator Point3D(Jungler.SQL.SqlNpc n) { return new Point3D(n); }

        /// <summary>
        /// type cast
        /// </summary>
        /// <param name="dod">DoodadObject</param>
        /// <returns></returns>
        public static implicit operator Point3D(DoodadObject dod) { return new Point3D(dod); }
        /// <summary>Class constructor overload</summary><param name="s">Self object, Core.me</param><seealso cref="Jungler.Bot.Classes.Self"/>
        public Point3D(Self s) { SetPoint(s); }
        /// <summary>Class constructor overload</summary><param name="s">Creatue object</param><seealso cref="Jungler.Bot.Classes.Creature"/>
        public Point3D(Creature s) { SetPoint(s); }
        /// <summary>Class constructor overload</summary><param name="s">GpsPoint type point</param><seealso cref="Jungler.Bot.Classes.GpsPoint"/>
        public Point3D(GpsPoint s) { SetPoint(s); }
        /// <summary>Class constructor overload</summary><param name="d">Doodad object</param><seealso cref="Jungler.Bot.Classes.DoodadObject"/>
        public Point3D(DoodadObject d) { SetPoint(d); }
        
        //  public Point3D(Jungler.SQL.SqlNpc n) { SetPoint(n); }

        /// <summary>
        /// set props from Self object
        /// </summary>
        /// <param name="cre"></param>
        public void SetPoint(Self cre) {
            this.X = cre.X;
            this.Y = cre.Y;
            this.Z = cre.Z;
            this.name = cre.name;
            this.type = 2;
            this.radius = cre.modelRadius;
        }
        /// <summary>
        /// set props from Doodad object
        /// </summary>
        /// <param name="cre"></param>
        public void SetPoint(DoodadObject cre) {
            this.X = cre.X;
            this.Y = cre.Y;
            this.Z = cre.Z;
            this.name = cre.name;
            this.type = 2;
            this.radius = cre.modelRadius;
        }
        /// <summary>
        /// set props from Creature object
        /// </summary>
        /// <param name="cre"></param>
        public void SetPoint(Creature cre) {
            this.X = cre.X;
            this.Y = cre.Y;
            this.Z = cre.Z;
            this.name = cre.name + "|" + cre.uniqId;
            this.type = 3;
            this.radius = cre.modelRadius;
        }
        /// <summary>
        /// set props from Creature object
        /// </summary>
        /// <param name="p"></param>
        public void SetPoint(GpsPoint p) {
            this.X = p.x;
            this.Y = p.y;
            this.Z = p.z;
            this.name = p.name;
            this.type = 5;
            this.radius = 1; // why no radius in GpsPoint if it exists in db?
        }
        
        /*
        public void SetPoint(Jungler.SQL.SqlNpc n) {
            //this.X = n.X;
            //this.Y = n.Y;
            //this.Z = n.Z;
            this.name = n.name;
            this.type = 5;
            this.radius = 1; // why no radius in GpsPoint if it exists in db?
        } */
        /// <summary>
        /// bred
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public double angle(Point3D b) {
            return Math.Atan2(b.Y, b.X) - Math.Atan2(this.Y, b.X);
        }
        /// <summary>
        /// square of value
        /// </summary>
        /// <param name="a">double</param>
        /// <returns>sqr</returns>
        public static double Sqr(double a) { return Math.Pow(a, 2); }
        /// <summary>
        /// square root of value
        /// </summary>
        /// <param name="a">double</param>
        /// <returns>sqrt</returns>
        public static double Sqrt(double a) { return Math.Pow(a, 2); }
        /// <summary>
        /// cos of angle in current point
        /// </summary>
        /// <param name="b">prevois point</param>
        /// <param name="c">next point</param>
        /// <returns>cos</returns>
        public double cos_azimut(Point3D b, Point3D c) {
            if (b == null) return 1;
            if (c == null) return 0;
            if (b == c || this.distNoZ(b) == 0 || this.distNoZ(c) == 0) return 1;
            return ((Sqr(this.distNoZ(b)) + Sqr(this.distNoZ(c)) - Sqr(b.distNoZ(c))) / (2 * this.distNoZ(b) * this.distNoZ(c)));
        }
        /// <summary>
        /// bred
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public double angleD(Point3D b) {
            return this.angle(b) * 180 / Math.PI;
        }
        /// <summary>
        /// dist to point ignoring Z ordinate
        /// </summary>
        /// <param name="b">second point</param>
        /// <returns>distance</returns>
        public double distNoZ(Point3D b) {
            return Math.Sqrt(Math.Pow((b.X - this.X), 2) + Math.Pow((b.Y - this.Y), 2));
        }
        /// <summary>
        /// distance between two points
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public double distZ(Point3D b) {
            return distZ(this, b);
        }

        /// <summary>
        /// distance between two points
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double distZ(Point3D a, Point3D b) {
            return Math.Sqrt(Math.Pow((b.X - a.X), 2) + Math.Pow((b.Y - a.Y), 2) + Math.Pow((b.Z - a.Z), 2));
        }
        /// <summary>
        /// Distance from point to node
        /// </summary>
        /// <param name="a">Point</param>
        /// <param name="b">Node</param>
        /// <returns></returns>
        public static double distZ(Point3D a, Node b) { if (a == null || b == null) return 999999999; return distZ(a, b.Position); }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double distZ(Node a, Node b) { return distZ(a.Position, b.Position); }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static double distZ(Node a, Point3D b) { return distZ(a.Position, b); }
        /// <summary>
        /// Returns the distance between two points.
        /// </summary>
        /// <param name="P1">First point.</param>
        /// <param name="P2">Second point.</param>
        /// <returns>Distance value.</returns>
        public static double DistanceBetween(Point3D P1, Point3D P2) {
            return Math.Sqrt(Math.Pow((P2.X - P1.X), 2) +
                Math.Pow((P2.Y - P1.Y), 2) +
                Math.Pow((P2.Z - P1.Z), 2));
            //return Math.Sqrt((P1.X-P2.X)*(P1.X-P2.X)+(P1.Y-P2.Y)*(P1.Y-P2.Y));
        }

        /// <summary>
        /// Returns the projection of a point on the line defined with two other points.
        /// When the projection is out of the segment, then the closest extremity is returned.
        /// </summary>
        /// <exception cref="ArgumentNullException">None of the arguments can be null.</exception>
        /// <exception cref="ArgumentException">P1 and P2 must be different.</exception>
        /// <param name="Pt">Point to project.</param>
        /// <param name="P1">First point of the line.</param>
        /// <param name="P2">Second point of the line.</param>
        /// <returns>The projected point if it is on the segment / The closest extremity otherwise.</returns>
        public static Point3D ProjectOnLine(Point3D Pt, Point3D P1, Point3D P2) {
            if (Pt == null || P1 == null || P2 == null) throw new ArgumentNullException("None of the arguments can be null.");
            if (P1.Equals(P2)) throw new ArgumentException("P1 and P2 must be different.");
            Vector3D VLine = new Vector3D(P1, P2);
            Vector3D V1Pt = new Vector3D(P1, Pt);
            Vector3D Translation = VLine * (VLine | V1Pt) / VLine.SquareNorm;
            Point3D Projection = P1 + Translation;

            Vector3D V1Pjt = new Vector3D(P1, Projection);
            double D1 = V1Pjt | VLine;
            if (D1 < 0) return P1;

            Vector3D V2Pjt = new Vector3D(P2, Projection);
            double D2 = V2Pjt | VLine;
            if (D2 > 0) return P2;

            return Projection;
        }

        /// <summary>
        /// Object.Equals override.
        /// Tells if two points are equal by comparing coordinates.
        /// </summary>
        /// <exception cref="ArgumentException">Cannot compare Point3D with another type.</exception>
        /// <param name="Point">The other 3DPoint to compare with.</param>
        /// <returns>'true' if points are equal.</returns>
        public override bool Equals(object Point) {
            Point3D P = (Point3D)Point;
            if (P == null) throw new ArgumentException("Object must be of type " + GetType());
            bool Resultat = true;
            for (int i = 0; i < 3; i++) Resultat &= P[i].Equals(this[i]);
            return Resultat;
        }

        /// <summary>
        /// Object.GetHashCode override.
        /// </summary>
        /// <returns>HashCode value.</returns>
        public override int GetHashCode() {
            double HashCode = 0;
            for (int i = 0; i < 3; i++) HashCode += this[i];
            return (int)HashCode;
        }

        /// <summary>
        /// Object.GetHashCode override.
        /// Returns a textual description of the point.
        /// </summary>
        /// <returns>String describing this point.</returns>
        public override string ToString() {
            string Deb = "{";
            string Sep = ";";
            string Fin = "}";
            string Nm = " name: ";
            string Resultat = Deb;
            int Dimension = 3;
            for (int i = 0; i < Dimension; i++)
                Resultat += _Coords[i].ToString() + (i != Dimension - 1 ? Sep : Fin);
            if (name != null) Resultat += Nm + name;
            return Resultat;
        }
    }
}
