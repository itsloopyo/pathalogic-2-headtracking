using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Pathologic2HeadTracking.Aim
{
    /// <summary>
    /// Turns a comma-separated list of layer NAMES into a physics mask.
    ///
    /// Names rather than numbers because the game's layer indices are project
    /// settings, not engine constants, and a number in an ini is unreadable and
    /// unverifiable. Names rather than an exclusion list because a blacklist can
    /// never be finished: the world is dense with interaction, area, cave and audio
    /// trigger volumes, and one missed name collapses a measured distance onto
    /// whatever volume the player is standing in.
    /// </summary>
    public struct LayerMaskResolver
    {
        /// <summary>Unity's physics layer count, and the width of every layer mask.</summary>
        public const int LayerCount = 32;

        public int Mask;

        /// <summary>Names that resolved, with the index each landed on.</summary>
        public string Resolved;

        /// <summary>Configured names this build of the game does not define.</summary>
        public string Unknown;

        public bool HasMask { get { return Mask != 0; } }

        public static LayerMaskResolver Resolve(string commaSeparatedNames)
        {
            var resolved = new List<string>();
            var unknown = new List<string>();
            int mask = 0;

            foreach (string raw in commaSeparatedNames.Split(','))
            {
                string name = raw.Trim();
                if (name.Length == 0) continue;

                int layer = LayerMask.NameToLayer(name);
                if (layer < 0)
                {
                    unknown.Add(name);
                    continue;
                }
                mask |= 1 << layer;
                resolved.Add(name + "(" + layer + ")");
            }

            return new LayerMaskResolver
            {
                Mask = mask,
                Resolved = string.Join(", ", resolved.ToArray()),
                Unknown = string.Join(", ", unknown.ToArray())
            };
        }

        /// <summary>Names of the layers set in a mask, for logging one.</summary>
        public static string Describe(int mask)
        {
            var sb = new StringBuilder();
            for (int layer = 0; layer < LayerCount; layer++)
            {
                if ((mask & (1 << layer)) == 0) continue;
                string name = LayerMask.LayerToName(layer);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(string.IsNullOrEmpty(name) ? layer.ToString() : name)
                  .Append('(').Append(layer).Append(')');
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        /// <summary>
        /// Every physics layer this build defines. The allow-lists above are only
        /// checkable against this, so it goes in the log once.
        /// </summary>
        public static string DescribeAllLayers()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < LayerCount; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(i).Append('=').Append(name);
            }
            return sb.ToString();
        }
    }
}
