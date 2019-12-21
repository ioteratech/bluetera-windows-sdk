using GlmSharp;
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

        public static Quaternion Inverse(this Quaternion q)
        {
            Quaternion qi = q;
            qi.Invert();
            return qi;
        }

        public static Quaternion CalcBodyToImuRotationFromPitch(Quaternion q1, Quaternion q2, bool snapToGrid = true)
        {
            // move to GlmSharp structs
            quat _q1 = new quat((float)q1.X, (float)q1.Y, (float)q1.Z, (float)q1.W);
            quat _q2 = new quat((float)q2.X, (float)q2.Y, (float)q2.Z, (float)q2.W);

            // normalize input quaternions
            _q1 = _q1.Normalized;
            _q2 = _q2.Normalized;

            // find rotation from Body frame to the Inertial frame
            quat q_axis = _q1 * _q2.Inverse;
            vec3 y_bi = q_axis.Axis;
            if (snapToGrid) y_bi[2] = 0.0f;
            y_bi = y_bi.Normalized;
            vec3 z_bi = new vec3(0.0f, 0.0f, 1.0f);
            vec3 x_bi = vec3.Cross(y_bi, z_bi).Normalized;

            mat3 R_ib = new mat3(x_bi, y_bi, z_bi);
            quat q_bi = R_ib.Transposed.ToQuaternion;

            // calculate the (fixed) rotation from the IMU frame to the Body frame
            quat q_bm = (q_bi * _q1).Inverse.Normalized;

            return new Quaternion(q_bm.x, q_bm.y, q_bm.z, q_bm.w);
        }
    };
}
