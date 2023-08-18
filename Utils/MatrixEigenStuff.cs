using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TPShipToolkit.Utils
{
    /// <summary>
    /// Methods to compute eigenvalues and eigenvectors from the covariance matrix of an object (a set of points).
    /// </summary>
    public static class MatrixEigenStuff
    {
        /// <summary>
        /// Calculate the eigenvectors from a 3x3 matrix and an eigenvalue. From <see href="https://stackoverflow.com/a/53470295"/>.
        /// </summary>
        /// <param name="row1">Row 1 of the input matrix.</param>
        /// <param name="row2">Row 2 of the input matrix.</param>
        /// <param name="lambda">The eigenvalue.</param>
        /// <returns></returns>
        public static Vector3 EigenVector(Vector3 row1, Vector3 row2, float lambda)
        {
            row1.X -= lambda;
            row2.Y -= lambda;
            row2 -= row1 * (row2.X / row1.X);
            var res = new Vector3(1f);
            res.Y = -row2.Z / row2.Y;
            res.X = -(row1.Y * res.Y + row1.Z * res.Z) / row1.X;
            return res;
        }

        /// <summary>
        /// Calculate the eigenvalues of a symetrical 3x3 matrix. From <see href="https://en.wikipedia.org/wiki/Eigenvalue_algorithm#3%C3%973_matrices"/>.
        /// </summary>
        /// <param name="row1">Row 1 of the matrix input.</param>
        /// <param name="row2">Row 2 of the matrix input.</param>
        /// <param name="row3">Row 3 of the matrix input.</param>
        /// <returns>A vector3 containing the 3 eigen values.</returns>
        public static Vector3 EigenValues(Vector3 row1, Vector3 row2, Vector3 row3)
        {
            var p1 = row1.Y * row1.Y + row1.Z * row1.Z + row2.Z * row2.Z;
            //if the matrix is not diagonal
            if (p1!=0)
            {
                var q = (row1.X + row2.Y + row3.Z) / 3;
                var p2 = (row1.X - q) * (row1.X - q) + (row2.Y - q) * (row2.Y - q) + (row3.Z - q) * (row3.Z - q) + 2 * p1;
                var p = Math.Sqrt(p2 / 6);
                Vector3 BRow1 = (row1 - q * Vector3.UnitX) / (float)p;
                Vector3 BRow2 = (row2 - q * Vector3.UnitY) / (float)p;
                Vector3 BRow3 = (row3 - q * Vector3.UnitZ) / (float)p;
                var r = Det(BRow1, BRow2, BRow3) / 2;
                var phi = r <= -1 ? Math.PI / 3 : (r >= 1 ? 0 : Math.Acos(r) / 3);
                Vector3 result = new Vector3();
                result.X = (float)(q + 2 * p * Math.Cos(phi));
                result.Z = (float)(q + 2 * p * Math.Cos(phi + (2 * Math.PI / 3)));
                result.Y = 3 * q - result.X - result.Z;
                return result;
            }
            //if the matrix is diagonal
            else
                return new Vector3(row1.X, row2.Y, row3.Z);
        }

        private static float Det(Vector3 row1, Vector3 row2, Vector3 row3)
        {
            return row1.X * row2.Y * row3.Z + row2.X * row3.Y * row1.Z + row1.Y * row2.Z * row3.X
                    - (row3.X * row2.Y * row1.Z + row2.X * row1.Y * row3.Z + row1.X * row3.Y * row2.Z);
        }
    }
}
