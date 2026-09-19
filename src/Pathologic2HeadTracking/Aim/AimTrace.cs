using System.Text;
using UnityEngine;

namespace Pathologic2HeadTracking.Aim
{
    /// <summary>Result of one aim cast along the clean camera forward.</summary>
    public struct AimResult
    {
        /// <summary>
        /// True when the cast actually ran. False means the allow-list resolved to
        /// nothing on this build, which reads identically to a clear 500m of air from
        /// the hit flag alone - and a reticle that has quietly lost its parallax term
        /// looks exactly like one that never had a surface to mark.
        /// </summary>
        public bool Queried;

        /// <summary>True when a bullet-blocking surface was found.</summary>
        public bool Hit;

        /// <summary>World position of the contact. Only meaningful when Hit.</summary>
        public Vector3 Point;

        /// <summary>Distance from the shot eye to the contact, along the aim direction.</summary>
        public float Distance;
    }

    /// <summary>
    /// Casts along the clean camera forward to find the surface a shot would stop on,
    /// so the reticle can be drawn at that surface's true depth rather than a guessed
    /// one. The depth is the whole size of the parallax correction, so which contact
    /// counts is the decision that matters here.
    /// </summary>
    public sealed class AimTrace
    {
        private const float MaxDistance = 500f;

        private readonly LayerMaskResolver _layers;
        private readonly LayerMaskResolver _npcHitLayers;

        public string ResolvedLayerNames { get { return _layers.Resolved; } }
        public string UnknownLayerNames { get { return _layers.Unknown; } }
        public bool HasMask { get { return _layers.HasMask; } }

        /// <summary>The NPC hit-collider layers, resolved, for the startup log line.</summary>
        public string ResolvedNpcHitLayerNames { get { return _npcHitLayers.Resolved; } }

        /// <param name="commaSeparatedLayerNames">Layers a shot stops on through solid geometry.</param>
        /// <param name="commaSeparatedNpcHitLayerNames">
        /// Layers whose TRIGGER colliders also stop a shot. Pathologic 2 puts its NPC
        /// hit boxes on triggers: RaycastAbilityProjectile casts with
        /// QueryTriggerInteraction.Collide and then discards every trigger hit whose
        /// layer is not GameSettingsData.NpcHitCollidersLayer. Ignoring triggers
        /// outright here would mark the wall behind a man rather than the man.
        /// </param>
        public AimTrace(string commaSeparatedLayerNames, string commaSeparatedNpcHitLayerNames)
        {
            _layers = LayerMaskResolver.Resolve(commaSeparatedLayerNames);
            _npcHitLayers = LayerMaskResolver.Resolve(commaSeparatedNpcHitLayerNames);
        }

        /// <summary>
        /// Casts from the clean shot eye along the clean aim direction, and returns the
        /// nearer of two contacts: the first solid surface on the geometry allow-list,
        /// and the first collider of any kind on the NPC hit-collider layers. That pair
        /// is the game's own rule for what a bullet stops on, split into two zero-extent
        /// rays so neither needs a RaycastAll and a sort.
        /// </summary>
        public AimResult Cast(Vector3 shotEye, Vector3 aimDirection)
        {
            AimResult result = default(AimResult);
            if (!_layers.HasMask) return result;

            result.Queried = true;

            RaycastHit solid;
            bool hitSolid = Physics.Raycast(shotEye, aimDirection, out solid, MaxDistance,
                _layers.Mask, QueryTriggerInteraction.Ignore);

            RaycastHit body = default(RaycastHit);
            bool hitBody = _npcHitLayers.HasMask
                           && Physics.Raycast(shotEye, aimDirection, out body, MaxDistance,
                               _npcHitLayers.Mask, QueryTriggerInteraction.Collide);

            if (!hitSolid && !hitBody) return result;

            bool bodyIsNearer = hitBody && (!hitSolid || body.distance < solid.distance);
            RaycastHit hit = bodyIsNearer ? body : solid;

            // Measured from the contact's own world position projected onto the aim
            // direction. That is a distance by construction, which hit.distance only
            // happens to be while this is a zero-extent ray.
            result.Hit = true;
            result.Point = hit.point;
            result.Distance = Vector3.Dot(hit.point - shotEye, aimDirection);
            return result;
        }

        /// <summary>
        /// What EVERY layer would have answered on its own, as "name=distance", with
        /// a leading '-' on the ones the allow-list excludes. This is the histogram
        /// the allow-list gets checked against: a taken layer whose distance is far
        /// shorter than the surface the player is looking at is a volume the ray
        /// should not be stopping on, and an excluded layer that consistently
        /// answers at the right distance is one the list is missing.
        ///
        /// Scans every layer, so it stays behind the diagnostics gate.
        /// </summary>
        public string DescribeContacts(Vector3 shotEye, Vector3 aimDirection)
        {
            var sb = new StringBuilder();
            for (int layer = 0; layer < LayerMaskResolver.LayerCount; layer++)
            {
                string name = LayerMask.LayerToName(layer);
                if (string.IsNullOrEmpty(name)) continue;

                Append(sb, name, layer, QueryTriggerInteraction.Ignore, _layers.Mask,
                    shotEye, aimDirection);
                // Second pass including triggers, so the NPC hit boxes - which are
                // triggers, and which a shot does stop on - are visible in the same
                // histogram as the geometry. A layer whose two passes disagree is one
                // whose contact is a volume.
                Append(sb, "t:" + name, layer, QueryTriggerInteraction.Collide,
                    _npcHitLayers.Mask, shotEye, aimDirection);
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        private static void Append(StringBuilder sb, string label, int layer,
            QueryTriggerInteraction triggers, int takenMask, Vector3 shotEye, Vector3 aimDirection)
        {
            RaycastHit hit;
            if (!Physics.Raycast(shotEye, aimDirection, out hit, MaxDistance, 1 << layer, triggers))
                return;

            if (sb.Length > 0) sb.Append(' ');
            if ((takenMask & (1 << layer)) == 0) sb.Append('-');
            sb.Append(label).Append('=').Append(hit.distance.ToString("F2"));
        }
    }
}
