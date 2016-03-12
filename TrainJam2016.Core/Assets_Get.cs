//
// Assets_Get.cs
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
using Urho.Audio;
using Urho;

namespace TrainJam2016
{
    public partial class Assets
    {
        static int lossSoundIndex = -1;
        static readonly string[] lossSounds = {
            Sounds.Cancel,
            Sounds.Cancel2,
            Sounds.Cancel3
        };

        public static Sound GetNextLossSound()
        {
            lossSoundIndex = (lossSoundIndex + 1) % lossSounds.Length;
            return Application.Current.ResourceCache.GetSound(lossSounds[lossSoundIndex]);
        }

        static int clickSoundIndex = -1;
        static readonly string[] clickSounds = {
            Sounds.Footstep1,
            Sounds.Footstep2,
            Sounds.Footstep3,
            Sounds.Footstep4,
            Sounds.Footstep5,
            Sounds.Footstep6,
            Sounds.Footstep7
        };

        public static Sound GetNextClickSound()
        {
            clickSoundIndex = (clickSoundIndex + 1) % clickSounds.Length;
            return Application.Current.ResourceCache.GetSound(clickSounds[clickSoundIndex]);
        }

        static int blockMaterialIndex = -1;
        static readonly string[] blockMaterials = {
            Materials.Block1,
            Materials.Block2,
            Materials.Block3,
            Materials.Block4,
            Materials.Block5
        };

        public static Material GetNextBlockMaterial()
        {
            blockMaterialIndex = (blockMaterialIndex + 1) % blockMaterials.Length;
            return Application.Current.ResourceCache.GetMaterial(blockMaterials[blockMaterialIndex]);
        }

    }
}

