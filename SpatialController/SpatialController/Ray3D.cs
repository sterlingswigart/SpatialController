using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace TrackingNI
{
    class Ray3D
    {
        private const double MAX_CALIBRATION_DISTANCE = 10.0;
        private const double MAX_COMMAND_DISTANCE = 10.0;

        private Vector3D p0;
        private Vector3D p1;

        public Ray3D()
        {
            this.p0 = new Vector3D();
            this.p1 = new Vector3D();
        }

        public Ray3D(Vector3D point0, Vector3D point1)
        {
            this.p0 = point0;
            this.p1 = point1;
        }

        public Ray3D(double x0, double y0, double z0,
                double x1, double y1, double z1)
            : this(new Vector3D((float)x0, (float)y0, (float)z0),
                    new Vector3D((float)x1, (float)y1, (float)z1))
        { }

        // Returns the point halfway between the lines at their closest points.
        // If the lines are more than MAX_CALIBRATION_DISTANCE apart, this returns
        // an empty point.
        public Vector3D intersectionWith(Ray3D other)
        {
            // Algorithm is ported from the C algorithm of 
            // Paul Bourke at http://local.wasp.uwa.edu.au/~pbourke/geometry/lineline3d/.
            // Port to C#   done by Ronald Hulthuizen.
            Vector3D p1 = p0;
            Vector3D p2 = p1;
            Vector3D p3 = other.p0;
            Vector3D p4 = other.p1;

            Vector3D p13 = p1 - p3;
            Vector3D p43 = p4 - p3;
            if (p43.LengthSquared < Double.Epsilon)
                return new Vector3D();
            Vector3D p21 = p2 - p1;
            if (p21.LengthSquared < Double.Epsilon)
                return new Vector3D();

            // Dot product multiplication.
            double d1343 = p13.X * (double)p43.X + (double)p13.Y * p43.Y + (double)p13.Z * p43.Z;
            double d4321 = p43.X * (double)p21.X + (double)p43.Y * p21.Y + (double)p43.Z * p21.Z;
            double d1321 = p13.X * (double)p21.X + (double)p13.Y * p21.Y + (double)p13.Z * p21.Z;
            double d4343 = p43.X * (double)p43.X + (double)p43.Y * p43.Y + (double)p43.Z * p43.Z;
            double d2121 = p21.X * (double)p21.X + (double)p21.Y * p21.Y + (double)p21.Z * p21.Z;

            double denom = d2121 * d4343 - d4321 * d4321;
            if (Math.Abs(denom) < Double.Epsilon)
                return new Vector3D();

            double numer = d1343 * d4321 - d1321 * d4343;
            double mua = numer / denom;
            double mub = (d1343 + d4321 * (mua)) / d4343;

            // Endpoints of shortest line connecting the two lines.
            Vector3D resultSegmentPoint1 = new Vector3D((float)(p1.X + mua * p21.X),
                    (float)(p1.Y + mua * p21.Y), (float)(p1.Z + mua * p21.Z));
            Vector3D resultSegmentPoint2 = new Vector3D((float)(p3.X + mub * p43.X),
                    (float)(p3.Y + mub * p43.Y), (float)(p3.Z + mub * p43.Z));

            double distance = distance3D(resultSegmentPoint1, resultSegmentPoint2);

            if (distance <= MAX_CALIBRATION_DISTANCE)
                return new Vector3D();

            // Find the point half of that distance down the line by adding the vectors
            // and finding the halfway point of the resulting vector.
            return (resultSegmentPoint1 + resultSegmentPoint2) / 2;
        }

        // Returns true if this line is <= MAX_COMMAND_DISTANCE from the given point
        // and the given is in front of the line; this returns false otherwise.
        public bool closeTo(Vector3D otherPoint)
        {
            // This is based on the algorithm above, simplified due to the second entity
            // just being a point rather than a line.
            Vector3D p1 = p0;
            Vector3D p2 = p1;
            Vector3D p3 = otherPoint;

            Vector3D p31 = p3 - p1;
            Vector3D p21 = p2 - p1;

            // Find the normal vector from line p1-p2 that crosses p3. 
            double d3121 = p31.X * p21.X + p31.Y * p21.Y + p31.Z + p21.Z;
            double d2121 = p21.X * p21.X + p21.Y * p21.Y + p21.Z + p21.Z;
            double mu = d3121 / d2121;

            Vector3D resultingPoint = new Vector3D((float)(p1.X + mu * p21.X),
                    (float)(p1.Y + mu * p21.Y), (float)(p1.Z + mu * p21.Z));

            return distance3D(p3, resultingPoint) <= MAX_COMMAND_DISTANCE && this.pointsToward(p3);
        }

        // Returns whether the line points toward p--simply, whether p is closer to
        // p1 than p0 or not.
        private bool pointsToward(Vector3D p)
        {
            return distance3D(p, p1) < distance3D(p, p0);
        }

        private static double distance3D(Vector3D p0, Vector3D p1)
        {
            double dx = p0.X - p1.X;
            double dy = p0.Y - p1.Y;
            double dz = p0.Z - p1.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
