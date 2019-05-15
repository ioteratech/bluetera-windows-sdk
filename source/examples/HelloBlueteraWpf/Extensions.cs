using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace HelloBlueteraWpf
{
    public static class QuaternionExtensions
    {
        /// <summary>
        /// Returns Euler Angles of the given Quaternion.
        /// </summary>
        /// <param name="q">The given Quaternion</param>
        /// <param name="isDegrees">Return angle in Degrees (default) or Radians</param>
        /// <returns>double[] - [phi (roll), theta (pitch), psi (yaw)]</returns>
        /// <see cref="https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles"/>
        public static double[] GetEuelerAngles(this Quaternion q, bool isDegrees = true)
        {
            double factor = isDegrees ? (180.0 / Math.PI) : 1.0;

            double phi = Math.Atan2(2.0 * (q.W * q.X + q.Y * q.Z), 1.0 - 2.0 * (q.X * q.X + q.Y * q.Y));
            double theta = Math.Asin(2.0 * (q.W * q.Y - q.Z * q.X));
            double psi = Math.Atan2(2.0 * (q.W * q.Z + q.X * q.Y), 1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z));

            return new double[] { factor * phi, factor * theta, factor * psi };
        }


    };
}
