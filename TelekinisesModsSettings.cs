using ThunderRoad;
using UnityEngine;

namespace TelekinisisOverhaul
{
    public static class TelekinesisModSettings
    {
        public static ModOptionBool[] BooleanOption =
        {
            new ModOptionBool("True", true),
            new ModOptionBool("False", false)
        };

        [ModOptionSlider]
        [ModOption("Overall Sensitivity",
            "How much hand movement = item movement (100% = 1:1 sphere scaling)",
            "sensitivityOption",
            category = "Telekinesis",
            defaultValueIndex = 50,
            order = 1)]
        public static float OverallSensitivity = 1f;

        [ModOptionSlider]
        [ModOption("Lateral Sensitivity",
            "Left/Right/Up/Down sensitivity (higher = wider strafing)",
            "sensitivityOption",
            category = "Telekinesis",
            defaultValueIndex = 50,
            order = 2)]
        public static float LateralSensitivity = 1f;

        [ModOptionSlider]
        [ModOption("Depth Sensitivity",
            "Forward/Back sensitivity (higher = easier long-range control)",
            "sensitivityOption",
            category = "Telekinesis",
            defaultValueIndex = 50,
            order = 3)]
        public static float DepthSensitivity = 1f;

        [ModOptionSlider]
        [ModOption("Local Sphere Radius",
            "How far you move your hand from your body to reach the Global Max Distance",
            "reachOption",
            category = "Telekinesis",
            defaultValueIndex = 38,
            order = 4)]
        public static float MaxReach = 0.65f;

        [ModOptionSlider]
        [ModOption("Global Max Distance",
            "Maximum distance an item can be held from your body",
            "maxDistanceOption",
            category = "Telekinesis",
            defaultValueIndex = 15,
            order = 5)]
        public static float MaxDistanceStatic = 15f;

        [ModOptionSlider]
        [ModOption("Flip Axis",
            "Which axis the menu button flips (Y=180° yaw, X=pitch, Z=roll)",
            "flipAxisOption",
            category = "Telekinesis",
            defaultValueIndex = 0,
            order = 6)]
        public static int FlipAxis = 0;

        [ModOptionButton]
        [ModOption("Show Telekinesis Lines",
            "Draws a line from the item to where you're aiming it",
            "booleanOption",
            category = "Telekinesis",
            order = 7)]
        public static bool LinesActive = false;

        [ModOptionButton]
        [ModOption("Enabled",
            "Turn the entire new telekinesis system on or off",
            "booleanOption",
            category = "Telekinesis",
            order = 8)]
        public static bool Enabled = true;

        [ModOptionSlider]
        [ModOption("Force Mode",
            "Force = mass-dependent, Acceleration = ignores mass, Impulse = instant mass-dependent, VelocityChange = instant ignores mass",
            "forceModeOption",
            category = "Telekinesis",
            defaultValueIndex = 0,
            order = 9)]
        public static int ForceModeIndex = 0;

        [ModOptionSlider]
        [ModOption("Force Multiplier",
            "How strong the telekinesis push feels when lerp is off (higher = snappier)",
            "forceMultiplierOption",
            category = "Telekinesis",
            defaultValueIndex = 500,
            order = 12)]
        public static float ForceMultiplier = 500f;

        [ModOptionSlider]
        [ModOption("Item Recall Min Speed",
            "How slow the item moves at light trigger press",
            "recallSpeedMinOption",
            category = "Recall",
            defaultValueIndex = 15,
            order = 1)]
        public static float RecallSpeedMin = 15f;

        [ModOptionSlider]
        [ModOption("Item Recall Max Speed",
            "How fast the item moves at full trigger press",
            "recallSpeedMaxOption",
            category = "Recall",
            defaultValueIndex = 105,
            order = 2)]
        public static float RecallSpeedMax = 525f;

        [ModOptionSlider]
        [ModOption("Ragdoll Recall Min Speed",
            "How slow enemies move at light trigger press",
            "recallSpeedMinOption",
            category = "Recall",
            defaultValueIndex = 15,
            order = 3)]
        public static float RagdollRecallSpeedMin = 15f;

        [ModOptionSlider]
        [ModOption("Ragdoll Recall Max Speed",
            "How fast enemies move at full trigger press",
            "recallSpeedMaxOption",
            category = "Recall",
            defaultValueIndex = 100,
            order = 4)]
        public static float RagdollRecallSpeedMax = 100f;

        [ModOptionButton]
        [ModOption("Recall Lerp Movement",
            "Smoothly lerp items/enemies toward you on recall, or use raw force mode",
            "booleanOption",
            category = "Recall",
            order = 5)]
        public static bool RecallLerpMovement = true;

        [ModOptionSlider]
        [ModOption("Recall Lerp Responsiveness",
            "How snappily items/enemies accelerate toward you during recall (only when Recall Lerp is on)",
            "recallLerpOption",
            category = "Recall",
            defaultValueIndex = 37,
            order = 6)]
        public static float RecallLerpResponsiveness = 375f;

        [ModOptionSlider]
        [ModOption("Recall Force Mode",
            "Force mode used when Recall Lerp is off",
            "forceModeOption",
            category = "Recall",
            defaultValueIndex = 0,
            order = 7)]
        public static int RecallForceModeIndex = 0;

        public static ForceMode GetForceMode()
        {
            switch (ForceModeIndex)
            {
                case 0: return ForceMode.Force;
                case 1: return ForceMode.Acceleration;
                case 2: return ForceMode.Impulse;
                case 3: return ForceMode.VelocityChange;
                default: return ForceMode.Force;
            }
        }

        public static ForceMode GetRecallForceMode()
        {
            switch (RecallForceModeIndex)
            {
                case 0: return ForceMode.Force;
                case 1: return ForceMode.Acceleration;
                case 2: return ForceMode.Impulse;
                case 3: return ForceMode.VelocityChange;
                default: return ForceMode.Force;
            }
        }

        public static ModOptionFloat[] SensitivityOption()
        {
            var array = new ModOptionFloat[201];
            for (var i = 0; i < array.Length; i++)
            {
                var value = i * 0.01f;
                array[i] = new ModOptionFloat((value * 100f).ToString("0") + "%", value);
            }

            return array;
        }

        public static ModOptionInt[] FlipAxisOption()
        {
            return new[]
            {
                new ModOptionInt("Yaw (Y-axis 180°)", 0),
                new ModOptionInt("Pitch (X-axis 180°)", 1),
                new ModOptionInt("Roll (Z-axis 180°)", 2)
            };
        }

        public static ModOptionInt[] ForceModeOption()
        {
            return new[]
            {
                new ModOptionInt("Force (mass-dependent)", 0),
                new ModOptionInt("Acceleration (ignores mass)", 1),
                new ModOptionInt("Impulse (instant, mass-dependent)", 2),
                new ModOptionInt("VelocityChange (instant, ignores mass)", 3)
            };
        }

        public static ModOptionFloat[] ReachOption()
        {
            var array = new ModOptionFloat[39];
            var num = 0.27f;
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = new ModOptionFloat(num.ToString("0.000"), num);
                num += 0.01f;
            }

            return array;
        }

        public static ModOptionFloat[] ForceMultiplierOption()
        {
            var array = new ModOptionFloat[501];
            var num = 0f;
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = new ModOptionFloat(num.ToString("0.0"), num);
                num += 3f;
            }

            return array;
        }

        public static ModOptionFloat[] MaxDistanceOption()
        {
            var array = new ModOptionFloat[151];
            var num = 0f;
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = new ModOptionFloat(num.ToString("0.0"), num);
                num += 1f;
            }

            return array;
        }


        public static ModOptionFloat[] RecallSpeedMinOption()
        {
            var array = new ModOptionFloat[201];
            var num = 0f;
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = new ModOptionFloat(num.ToString("0.0"), num);
                num += 1f;
            }

            return array;
        }

        public static ModOptionFloat[] RecallSpeedMaxOption()
        {
            var array = new ModOptionFloat[201];
            var num = 0f;
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = new ModOptionFloat(num.ToString("0.0"), num);
                num += 5f;
            }

            return array;
        }

        public static ModOptionFloat[] RecallLerpOption()
        {
            var array = new ModOptionFloat[101];
            var num = 0f;
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = new ModOptionFloat(num.ToString("0.0"), num);
                num += 10f;
            }

            return array;
        }
    }
}