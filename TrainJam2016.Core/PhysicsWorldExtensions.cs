//
// PhysicsWorldExtensions.cs
//
// Author:
//       mhutch <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2016 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Runtime.InteropServices;
using Urho;
using Urho.Physics;
using Urho.Resources;

namespace TrainJam2016
{
    static class PhysicsWorldExtensions
    {
        [DllImport("mono-urho", CallingConvention = CallingConvention.Cdecl)]
        static extern void PhysicsWorld_RaycastSingle(IntPtr handle, ref FixedRaycastResult result, ref Ray ray, float maxDistance, uint collisionMask);

        struct FixedRaycastResult
        {
            public Vector3 Position, Normal;
            public float Distance, HitFraction;
            public IntPtr bodyPtr;
        }

        struct BrokenRaycastResult
        {
            public Vector3 Position, Normal;
            public float Distance;
            public IntPtr bodyPtr;
        }

        //PhysicsWorld.RaycastSingle crashes because PhysicsRaycastResult is missing the bodyPtr field
        public static void RaycastSingleNoCrash(this PhysicsWorld world, ref PhysicsRaycastResult result, Ray ray, float maxDistance, uint collisionMask)
        {
            var r = new FixedRaycastResult();
            PhysicsWorld_RaycastSingle(world.Handle, ref r, ref ray, maxDistance, collisionMask);

            var proxy = new PhysicsRaycastResult();
            unsafe
            {
                var ptr = (BrokenRaycastResult*) &proxy;
                ptr->Position = r.Position;
                ptr->Normal = r.Normal;
                ptr->Distance = r.Distance;
                ptr->bodyPtr = r.bodyPtr;
            }

            result = proxy;
        }
    }
}
