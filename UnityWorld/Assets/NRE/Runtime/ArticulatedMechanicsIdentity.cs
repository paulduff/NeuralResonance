using System;
using System.Linq;
using UnityEngine;

namespace Nre.UnityWorld
{
    // Supported native scene configuration, separate from evolving body state.
    // Unknown collider/dynamic-body kinds reject; this is not a general scene exporter.
    internal static class ArticulatedMechanicsIdentity
    {
        [Serializable] sealed class Scene
        {
            public string[] physics;
            public bool[] layerIgnored;
            public Shape[] shapes;
            public bool[] ignoredPairs;
            public Body[] bodies;
        }
        [Serializable] sealed class Pose { public string name; public string[] position, rotation, scale; }
        [Serializable] sealed class Shape
        {
            public string name, kind, body;
            public int instance, layer, priority;
            public uint include, exclude;
            public bool enabled, active, trigger;
            public string[] dimensions, material;
            public Pose[] transforms;
            public string contactOffset;
        }
        [Serializable] sealed class Body
        {
            public string name;
            public int instance;
            public string[] parameters;
        }
        static string B(float x) => MuscleHingeWire.Encode(x);
        static string[] V(Vector3 x) => new[] { B(x.x), B(x.y), B(x.z) };
        static string[] Q(Quaternion x) => new[] { B(x.x), B(x.y), B(x.z), B(x.w) };
        static string Key(Transform t) => t == null ? "" : Key(t.parent) + "/" + t.name + "[" + t.GetSiblingIndex() + "]";
        static Pose[] Chain(Transform t, Transform stop)
        {
            var chain = new System.Collections.Generic.List<Pose>();
            for (; t != stop && t != null; t = t.parent)
                chain.Add(new Pose { name = Key(t), position = V(t.localPosition), rotation = Q(t.localRotation), scale = V(t.localScale) });
            return chain.ToArray();
        }
        internal static string Capture(ArticulatedAvatar avatar)
        {
            if (Physics.simulationMode != SimulationMode.Script) throw new InvalidOperationException("Explicit physics owner required.");
            var bodies = UnityEngine.Object.FindObjectsByType<ArticulationBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bodies.Length != avatar.links.Count || bodies.Any(b => !avatar.links.Values.Any(l => l.body == b)) ||
                UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0)
                throw new InvalidOperationException("Only this declared articulated body and static scenery are supported.");
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(c => Key(c.transform), StringComparer.Ordinal).ToArray();
            if (colliders.Select(c => Key(c.transform)).Distinct().Count() != colliders.Length)
                throw new InvalidOperationException("Require one collider per declared shape transform.");
            var shapes = colliders.Select(c => {
                var attached = c.attachedArticulationBody;
                string[] dimensions;
                if (c is BoxCollider box) dimensions = V(box.center).Concat(V(box.size)).ToArray();
                else if (c is CapsuleCollider capsule) dimensions = V(capsule.center).Concat(new[] { B(capsule.radius), B(capsule.height), capsule.direction.ToString() }).ToArray();
                else if (c is SphereCollider sphere) dimensions = V(sphere.center).Concat(new[] { B(sphere.radius) }).ToArray();
                else throw new InvalidOperationException("Unsupported collider: " + c.GetType().Name);
                var m = c.sharedMaterial;
                return new Shape { name = Key(c.transform), kind = c.GetType().Name, instance = c.GetInstanceID(),
                    body = attached == null ? "static" : attached.name, enabled = c.enabled, active = c.gameObject.activeInHierarchy,
                    layer = c.gameObject.layer, trigger = c.isTrigger, contactOffset = B(c.contactOffset),
                    include = unchecked((uint)c.includeLayers.value), exclude = unchecked((uint)c.excludeLayers.value), priority = c.layerOverridePriority,
                    dimensions = dimensions, transforms = Chain(c.transform, attached == null ? null : attached.transform),
                    // A null reference explicitly selects this pinned engine's default material.
                    material = m == null ? new[] { "pinned-unity-default-material" } : new[] { B(m.dynamicFriction), B(m.staticFriction), B(m.bounciness), m.frictionCombine.ToString(), m.bounceCombine.ToString() } };
            }).ToArray();
            var pairs = new System.Collections.Generic.List<bool>();
            for (int i = 0; i < colliders.Length; i++) for (int j = i + 1; j < colliders.Length; j++) pairs.Add(Physics.GetIgnoreCollision(colliders[i], colliders[j]));
            // Capture the installed setting even though Unity now discourages its use.
#pragma warning disable CS0618
            bool autoSync = Physics.autoSyncTransforms;
#pragma warning restore CS0618
            return JsonUtility.ToJson(new Scene {
                physics = V(Physics.gravity).Concat(new[] { Physics.simulationMode.ToString(), B(Physics.bounceThreshold), B(Physics.defaultContactOffset),
                    B(Physics.sleepThreshold), B(Physics.defaultMaxDepenetrationVelocity), Physics.defaultSolverIterations.ToString(),
                    Physics.defaultSolverVelocityIterations.ToString(), autoSync.ToString(), Physics.reuseCollisionCallbacks.ToString() }).ToArray(),
                layerIgnored = Enumerable.Range(0, 32).SelectMany(i => Enumerable.Range(0, 32).Select(j => Physics.GetIgnoreLayerCollision(i, j))).ToArray(), shapes = shapes, ignoredPairs = pairs.ToArray(),
                bodies = avatar.links.Values.OrderBy(l => l.name, StringComparer.Ordinal).Select(l => new Body { name = l.name, instance = l.body.GetInstanceID(),
                    parameters = new[] { B(l.body.mass), B(l.body.linearDamping), B(l.body.angularDamping), B(l.body.jointFriction), B(l.body.sleepThreshold),
                        B(l.body.maxAngularVelocity), B(l.body.maxLinearVelocity), B(l.body.maxDepenetrationVelocity), l.body.collisionDetectionMode.ToString(),
                        l.body.useGravity.ToString(), l.body.immovable.ToString(), l.body.gameObject.activeInHierarchy.ToString() }
                        .Concat(V(l.body.inertiaTensor)).Concat(Q(l.body.inertiaTensorRotation)).Concat(V(l.body.centerOfMass))
                        .Concat(V(l.body.anchorPosition)).Concat(Q(l.body.anchorRotation)).Concat(V(l.body.parentAnchorPosition)).Concat(Q(l.body.parentAnchorRotation)).ToArray() }).ToArray()
            });
        }
    }
}
