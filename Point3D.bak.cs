using System;
using Jungler.Bot.Classes;


namespace Dwader.Navigation
{
    /// <summary>3D point class</summary>    
    [Serializable]
    public class Point3D
    {
        double[] _Coordinates = new double[3];
        /// <summary>
        /// X ordinate
        /// </summary>
        public double X { set { _Coordinates[0] = value; } get { return _Coordinates[0]; } }
        /// <summary>
        /// Y  ordinate
        /// </summary>
        public double Y { set { _Coordinates[1] = value; } get { return _Coordinates[1]; } }
        /// <summary>
        /// Z  ordinate
        /// </summary>
        public double Z { set { _Coordinates[2] = value; } get { return _Coordinates[2]; } }
        /// <summary>
        /// optional name
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// internal type
        /// </summary>
        public int type { get; set; }
        /// <summary>
        /// for debug
        /// </summary>
        private string err { get; set; }
        /// <summary>
        /// dist to next ???
        /// </summary>
        public double ndist = 0;
        /// <summary>
        /// radius of point (usually modelRadius)
        /// </summary>
        public double radius { get; set; }

        /// <summary>
        /// Point3D constructor.
        /// </summary>
        /// <exception cref="ArgumentNullException">Argument array must not be null.</exception>
        /// <exception cref="ArgumentException">The Coordinates' array must contain exactly 3 elements.</exception>
        /// <param name="Coordinates">An array containing the three coordinates' values.</param>
        public Point3D(double[] Coordinates)
        {
            if (Coordinates == null) throw new ArgumentNullException();
            if (Coordinates.Length != 3) throw new ArgumentException("The Coordinates' array must contain exactly 3 elements.");
            X = Coordinates[0]; Y = Coordinates[1]; Z = Coordinates[2];
        }

        /// <summary>Class constructor</summary>
        /// <param name="x">X ordinate</param>
        /// <param name="y">Y ordinate</param>
        /// <param name="z">Z ordinate</param>        
        /// <param name="name">optional name of point</param>
        public Point3D(double x, double y, double z, string name)
        {
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
        /// <summary>Class constructor overload</summary><param name="s">Self object, Core.me</param><seealso cref="Jungler.Bot.Classes.Self"/>
        public Point3D(Self s) { SetPoint(s); }
        /// <summary>Class constructor overload</summary><param name="s">Creatue object</param><seealso cref="Jungler.Bot.Classes.Creature"/>
        public Point3D(Creature s) { SetPoint(s); }
        /// <summary>Class constructor overload</summary><param name="s">GpsPoint type poin</param><seealso cref="Jungler.Bot.Classes.GpsPoint"/>
        public Point3D(GpsPoint s) { SetPoint(s); }
        /// <summary>
        /// set props from Self object
        /// </summary>
        /// <param name="cre"></param>
        public void SetPoint(Self cre)
        {
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
        public void SetPoint(Creature cre)
        {
            this.X = cre.X;
            this.Y = cre.Y;
            this.Z = cre.Z;
            this.name = cre.name;
            this.type = 3;
            this.radius = cre.modelRadius;
        }
        /// <summary>
        /// set props from Creature object
        /// </summary>
        /// <param name="p"></param>
        public void SetPoint(GpsPoint p)
        {
            this.X = p.x;
            this.Y = p.y;
            this.Z = p.z;
            this.name = p.name;
            this.type = 5;
            this.radius = 1; // why no radius in GpsPoint if it exists in db?
        }
        /// <summary>
        /// overload ToString
        /// </summary>
        /// <returns>formatted set of props</returns>
        public override string ToString()
        {
            return "" + this.X + "," + this.Y + "," + this.Z + "," + this.name + " # " + this.type + ">" + this.ndist + " " + this.err;
        }
        /// <summary>
        /// bred
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public double angle(Point3D b)
        {
            return Math.Atan2(b.Y, b.X) - Math.Atan2(this.Y, b.X);
        }
        /// <summary>
        /// square of value
        /// </summary>
        /// <param name="a">double</param>
        /// <returns>sqr</returns>
        public double Sqr(double a) { return Math.Pow(a, 2); }
        /// <summary>
        /// square root of value
        /// </summary>
        /// <param name="a">double</param>
        /// <returns>sqrt</returns>
        public double Sqrt(double a) { return Math.Pow(a, 2); }
        /// <summary>
        /// cos of angle in current point
        /// </summary>
        /// <param name="b">prevois point</param>
        /// <param name="c">next point</param>
        /// <returns>cos</returns>
        public double cos_azimut(Point3D b, Point3D c)
        {
            if (b == null) return 1;
            if (c == null) return 0;
            return ((Sqr(this.dist(b)) + Sqr(this.dist(c)) - Sqr(b.dist(c))) / (2 * this.dist(b) * this.dist(c)));
        }
        /// <summary>
        /// bred
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public double angleD(Point3D b)
        {
            return this.angle(b) * 180 / Math.PI;
        }
        /// <summary>
        /// distance between two points
        /// </summary>
        /// <param name="b">second point</param>
        /// <returns>distance</returns>
        public double dist(Point3D b)
        {
            return Math.Sqrt(Math.Pow((b.X - this.X), 2) + Math.Pow((b.Y - this.Y), 2));
        }
        /// <summary>
        /// dist to point ignoring Z ordinate
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public double distZ(Point3D b)
        {
            return Math.Sqrt(Math.Pow((b.X - this.X), 2) + Math.Pow((b.Y - this.Y), 2) + Math.Pow((b.Z - this.Z), 2));
        }
        /// <summary>
        /// Object.GetHashCode override.
        /// </summary>
        /// <returns>HashCode value.</returns>
        public override int GetHashCode()
        {
            double HashCode = 0;
            for (int i = 0; i < 3; i++) HashCode += this[i];
            return (int)HashCode;
        }
        /// <summary>
        /// Object.Equals override.
        /// Tells if two points are equal by comparing coordinates.
        /// </summary>
        /// <exception cref="ArgumentException">Cannot compare Point3D with another type.</exception>
        /// <param name="Point">The other 3DPoint to compare with.</param>
        /// <returns>'true' if points are equal.</returns>
        public override bool Equals(object Point)
        {
            Point3D P = (Point3D)Point;
            if (P == null) throw new ArgumentException("Object must be of type " + GetType());
            bool Resultat = true;
            for (int i = 0; i < 3; i++) Resultat &= P[i].Equals(this[i]);
            return Resultat;
        }

    }
}
