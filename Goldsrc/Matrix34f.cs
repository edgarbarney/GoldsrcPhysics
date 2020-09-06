﻿using BulletSharp.Math;
using System.Runtime.InteropServices;

namespace GoldsrcPhysics.Goldsrc
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct Matrix34f
    {
        [FieldOffset(0)]
        public BulletSharp.Math.Vector4 Row1;
        [FieldOffset(16)]
        public BulletSharp.Math.Vector4 Row2;
        [FieldOffset(32)]
        public BulletSharp.Math.Vector4 Row3;
        [FieldOffset(0)]
        public fixed float M[3 * 4];

        public BulletSharp.Math.Vector3 Origin { get => new Vector3(M[3], M[7], M[11]); }

        public static Matrix34f Identity
        {
            get
            {
                var result = new Matrix34f();
                result.M[0] = 1;
                result.M[1 * 4 + 1] = 1;
                result.M[2 * 4 + 2] = 1;
                return result;
            }

        }
        public float this[int i] => M[i];

        public static Matrix34f Zero => new Matrix34f();

        //public Vector3 IHat { get=>new Vector3(M[0*0],M[ }
        //public Vector3 JHat { get; }
        //public Vector3 KHat { get; }
        //public Vector3 Origin;


        public static implicit operator BulletSharp.Math.Matrix(Matrix34f matrix)
        {
            return new BulletSharp.Math.Matrix(
                matrix[0], matrix[4], matrix[8], 0,
                matrix[1], matrix[5], matrix[9], 0,
                matrix[2], matrix[6], matrix[10], 0,
                matrix[3], matrix[7], matrix[11], 1
                );
        }
        public static implicit operator LinearMath.Matrix(Matrix34f matrix)
        {
            return new LinearMath.Matrix(
                matrix[0], matrix[4], matrix[8], 0,
                matrix[1], matrix[5], matrix[9], 0,
                matrix[2], matrix[6], matrix[10], 0,
                matrix[3], matrix[7], matrix[11], 1
                );
        }
        public static implicit operator Matrix34f(BulletSharp.Math.Matrix m)
        {
            return new Matrix34f()
            {
                Row1 = m.Column1,
                Row2 = m.Column2,
                Row3 = m.Column3
            };
        }
        public static unsafe implicit operator Matrix34f(LinearMath.Matrix m)
        {
            var col1 = m.Column1;
            var col2 = m.Column2;
            var col3 = m.Column3;
            return new Matrix34f()
            {

                Row1 = *((Vector4*)&col1),
                Row2 = *((Vector4*)&col2),
                Row3 = *((Vector4*)&col3)
            };
        }
        
        /// <summary>
        /// 将儿子的局部变换转化为世界变换，用的是矩阵乘法
        /// 
        /// </summary>
        /// <param name="lhs">parent world transform</param>
        /// <param name="rhs">child's local transform</param>
        /// <param name="res">child's world transform</param>
        public static void ConcatTransforms(in Matrix34f lhs, in Matrix34f rhs, out Matrix34f result)
        {
            Matrix34f res = new Matrix34f();
            res.M[0] = lhs[0 * 4 + 0] * rhs[0 * 4 + 0] + lhs[0 * 4 + 1] * rhs[1 * 4 + 0] +
                lhs[0 * 4 + 2] * rhs[2 * 4 + 0];
            res.M[1] = lhs[0 * 4 + 0] * rhs[0 * 4 + 1] + lhs[0 * 4 + 1] * rhs[1 * 4 + 1] +
                lhs[0 * 4 + 2] * rhs[2 * 4 + 1];
            res.M[2] = lhs[0 * 4 + 0] * rhs[0 * 4 + 2] + lhs[0 * 4 + 1] * rhs[1 * 4 + 2] +
                lhs[0 * 4 + 2] * rhs[2 * 4 + 2];
            res.M[3] = lhs[0 * 4 + 0] * rhs[0 * 4 + 3] + lhs[0 * 4 + 1] * rhs[1 * 4 + 3] +
                lhs[0 * 4 + 2] * rhs[2 * 4 + 3] + lhs[0 * 4 + 3];
            res.M[4] = lhs[1 * 4 + 0] * rhs[0 * 4 + 0] + lhs[1 * 4 + 1] * rhs[1 * 4 + 0] +
                lhs[1 * 4 + 2] * rhs[2 * 4 + 0];
            res.M[5] = lhs[1 * 4 + 0] * rhs[0 * 4 + 1] + lhs[1 * 4 + 1] * rhs[1 * 4 + 1] +
                lhs[1 * 4 + 2] * rhs[2 * 4 + 1];
            res.M[6] = lhs[1 * 4 + 0] * rhs[0 * 4 + 2] + lhs[1 * 4 + 1] * rhs[1 * 4 + 2] +
                lhs[1 * 4 + 2] * rhs[2 * 4 + 2];
            res.M[7] = lhs[1 * 4 + 0] * rhs[0 * 4 + 3] + lhs[1 * 4 + 1] * rhs[1 * 4 + 3] +
                lhs[1 * 4 + 2] * rhs[2 * 4 + 3] + lhs[1 * 4 + 3];
            res.M[8] = lhs[2 * 4 + 0] * rhs[0 * 4 + 0] + lhs[2 * 4 + 1] * rhs[1 * 4 + 0] +
                lhs[2 * 4 + 2] * rhs[2 * 4 + 0];
            res.M[9] = lhs[2 * 4 + 0] * rhs[0 * 4 + 1] + lhs[2 * 4 + 1] * rhs[1 * 4 + 1] +
                lhs[2 * 4 + 2] * rhs[2 * 4 + 1];
            res.M[10] = lhs[2 * 4 + 0] * rhs[0 * 4 + 2] + lhs[2 * 4 + 1] * rhs[1 * 4 + 2] +
                lhs[2 * 4 + 2] * rhs[2 * 4 + 2];
            res.M[11] = lhs[2 * 4 + 0] * rhs[0 * 4 + 3] + lhs[2 * 4 + 1] * rhs[1 * 4 + 3] +
                lhs[2 * 4 + 2] * rhs[2 * 4 + 3] + lhs[2 * 4 + 3];
            result = res;
        }
    }

}
