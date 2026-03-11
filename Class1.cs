using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TelekinisisOverhaul
{
    public class GestureTelekinesisScript : ThunderScript
    {
        private const float RecallCooldown     = 0.4f;
        private const float TriggerDeadzone    = 0.005f;
        private const float RecallSnapDistance = 0.25f;
        private const float RecallSnapTrigger  = 0.90f;
        private const float GrabRadius         = 0.35f;
        private const float RecallFadeTime     = 0.2f;

        private struct FrozenTarget
        {
            private Item        _item;
            private RagdollPart _ragdollPart;
            private bool        _hadCharacterJoint;

            private Vector3    _position;
            private Quaternion _rotation;

            public bool IsValid   => _item != null || _ragdollPart != null;
            public bool IsRagdoll => _ragdollPart != null;

            public bool MatchesCurrentTarget(Handle handle)
            {
                if (handle == null) return false;
                if (_item != null) return _item == handle.item;
                if (_ragdollPart != null)
                {
                    var rh = handle as HandleRagdoll;
                    return rh?.ragdollPart == _ragdollPart;
                }
                return false;
            }

            public bool IsHeld()
            {
                if (_item != null)
                    return _item.mainHandler != null
                        || _item.handlers.Count > 0
                        || _item.tkHandlers.Count > 0;

                if (_ragdollPart != null)
                    return _ragdollPart.isGrabbed
                        || _ragdollPart.handles.Any(h => h?.MainTkHandler != null);

                return false;
            }

            public void Hold()
            {
                if (_item != null)
                {
                    var rb = _item.physicBody?.rigidBody;
                    if (rb == null) return;
                    rb.isKinematic = true;
                    _item.transform.SetPositionAndRotation(_position, _rotation);
                }
                else if (_ragdollPart != null)
                {
                    var rb = _ragdollPart.physicBody?.rigidBody;
                    if (rb == null) return;
                    rb.isKinematic = true;
                    _ragdollPart.transform.SetPositionAndRotation(_position, _rotation);
                }
            }

            public void Release()
            {
                if (_item != null)
                {
                    RestoreRb(_item.physicBody?.rigidBody);
                    _item = null;
                }
                else if (_ragdollPart != null)
                {
                    FrozenTarget.RestoreJointLimits(_ragdollPart);
                    _ragdollPart.ResetCharJointLimit();

                    var rb = _ragdollPart.physicBody?.rigidBody;
                    if (rb != null)
                    {
                        RestoreRb(rb);
                        _ragdollPart.physicBody.velocity        = Vector3.zero;
                        _ragdollPart.physicBody.angularVelocity = Vector3.zero;
                        RestoreJointLimits(_ragdollPart);
                    }
                    _ragdollPart = null;
                }
            }

            public static FrozenTarget FromItem(Handle handle) => new FrozenTarget
            {
                _item     = handle.item,
                _position = handle.item.transform.position,
                _rotation = handle.item.transform.rotation,
            };

            public static FrozenTarget FromRagdollPart(RagdollPart part) => new FrozenTarget
            {
                _ragdollPart = part,
                _position    = part.transform.position,
                _rotation    = part.transform.rotation,
            };

            private static void RestoreRb(Rigidbody rb)
            {
                if (rb == null) return;
                rb.isKinematic     = false;
                rb.velocity        = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            public static void RestoreJointLimits(RagdollPart part)
            {
                if (part?.characterJoint == null || part.characterJointLocked) return;
                part.characterJoint.breakForce  = part.orgCharacterJointData.breakForce;
                part.characterJoint.breakTorque = part.orgCharacterJointData.breakTorque;
            }
        }

        private struct HandState
        {
            public SpellCaster  caster;
            public Rigidbody    grip;
            public LineRenderer line;

            public Quaternion desiredRot;
            public Quaternion lockedHandRot;
            public Quaternion lockedItemRot;
            public bool       rotLocked;
            public bool       rotDirty;
            public bool       inverted;

            public bool  recallActive;
            public float recallTimer;

            public bool wasMenu;
            public bool wasFreezeChord;

            public List<FrozenTarget> frozenTargets;

            public void Reset()
            {
                desiredRot     = Quaternion.identity;
                lockedHandRot  = Quaternion.identity;
                lockedItemRot  = Quaternion.identity;
                rotLocked      = false;
                rotDirty       = false;
                inverted       = false;
                recallActive   = false;
                recallTimer    = 0f;
                wasMenu        = false;
                wasFreezeChord = false;
                frozenTargets  = new List<FrozenTarget>();
            }
        }

        private HandState _left;
        private HandState _right;

        private Transform _neck;
        private float     _maxDistance;
        private float     _lastReleaseTime;
        private bool      _linesCreated;
        private bool      _initialized;

        private readonly Vector3[] _linePoints = new Vector3[2];

        public override void ScriptEnable()
        {
            base.ScriptEnable();
            EventManager.onPossess += OnPossess;
        }

        public override void ScriptDisable()
        {
            base.ScriptDisable();
            _initialized = false;
            EventManager.onPossess -= OnPossess;
        }

        private void OnPossess(Creature creature, EventTime eventTime)
        {
            if (creature?.player?.creature != null)
            {
                Initialize(creature.player.creature);
                _initialized = true;
            }
        }

        public override void ScriptFixedUpdate()
        {
            base.ScriptFixedUpdate();
            if (!_initialized) return;

            SyncLineRenderers();
            TickHand(ref _right);
            TickHand(ref _left);
        }

        public override void ScriptUpdate()
        {
            base.ScriptUpdate();
            if (!_initialized) return;

            UpdateRotation(ref _right);
            UpdateRotation(ref _left);

            TickFrozenList(ref _left);
            TickFrozenList(ref _right);
        }

        public override void ScriptLateUpdate()
        {
            base.ScriptLateUpdate();
            if (!_initialized) return;

            ApplyRotation(ref _right);
            ApplyRotation(ref _left);
        }

        public void Initialize(Creature player)
        {
            _maxDistance     = TelekinesisModSettings.MaxDistanceStatic;
            _lastReleaseTime = 0f;
            _neck            = player.animator.GetBoneTransform(HumanBodyBones.Neck);

            InitHand(ref _left,  player.handLeft.caster);
            InitHand(ref _right, player.handRight.caster);

            if (TelekinesisModSettings.LinesActive) MakeLines();
        }

        private static void InitHand(ref HandState hand, SpellCaster caster)
        {
            hand.Reset();
            hand.caster                                  = caster;
            hand.caster.telekinesis.pullAndRepelMaxSpeed = 0f;
            hand.caster.telekinesis.positionSpring       = 0f;
            hand.grip                                    = FindGrip(caster);
        }

        private static Rigidbody FindGrip(SpellCaster caster)
        {
            if (caster == null) return null;
            var t = caster.transform.Find("TkGrip");
            return t != null ? t.GetComponent<Rigidbody>() : null;
        }

        private void TickHand(ref HandState hand)
        {
            if (hand.caster?.telekinesis?.catchedHandle == null) return;
            if (!TelekinesisModSettings.Enabled) return;

            var control     = PlayerControl.GetHand(hand.caster.ragdollHand.side);
            var triggerAxis = control.useAxis;
            var triggerHeld = triggerAxis > TriggerDeadzone;
            var chordHeld   = control.alternateUsePressed && triggerAxis > TriggerDeadzone;

            HandleFreezeChord(ref hand, triggerAxis, chordHeld);

            var state = hand;
            if (hand.frozenTargets.Count > 0 &&
                hand.frozenTargets.Any(f => f.MatchesCurrentTarget(state.caster.telekinesis.catchedHandle)))
                return;

            HandleRecallRelease(ref hand, triggerHeld);
            HandleRecallOrFloat(ref hand, triggerAxis, triggerHeld, chordHeld);
        }

        private void HandleFreezeChord(ref HandState hand, float triggerAxis, bool chordHeld)
        {
            var chordActive   = triggerAxis > TriggerDeadzone && chordHeld;
            var justActivated = chordActive && !hand.wasFreezeChord;
            hand.wasFreezeChord = chordActive;

            if (!justActivated) return;

            var state = hand;
            var existingIndex = hand.frozenTargets.FindIndex(
                f => f.MatchesCurrentTarget(state.caster.telekinesis.catchedHandle));

            if (existingIndex >= 0)
            {
                hand.frozenTargets[existingIndex].Release();
                hand.frozenTargets.RemoveAt(existingIndex);
                hand.caster.ragdollHand?.playerHand?.controlHand?.HapticShort(0.5f);
            }
            else
            {
                FreezeCurrentTarget(ref hand);
            }
        }

        private void FreezeCurrentTarget(ref HandState hand)
        {
            var handle = hand.caster.telekinesis.catchedHandle;
            if (handle == null) return;

            var ragdollHandle = handle as HandleRagdoll;
            var ragdoll       = ragdollHandle?.ragdollPart?.ragdoll;

            if (ragdoll != null)
            {
                var part = ragdollHandle.ragdollPart;
                hand.frozenTargets.Add(FrozenTarget.FromRagdollPart(part));

                if (part?.physicBody?.rigidBody != null)
                {
                    part.physicBody.velocity              = Vector3.zero;
                    part.physicBody.angularVelocity       = Vector3.zero;
                    part.physicBody.rigidBody.isKinematic = true;
                    part.FixedCharJointLimit();
                    if (part.characterJoint != null)
                    {
                        part.characterJoint.breakForce  = Mathf.Infinity;
                        part.characterJoint.breakTorque = Mathf.Infinity;
                    }
                }
            }
            else if (handle.item != null && handle.physicBody?.rigidBody != null)
            {
                hand.frozenTargets.Add(FrozenTarget.FromItem(handle));
                handle.physicBody.velocity              = Vector3.zero;
                handle.physicBody.angularVelocity       = Vector3.zero;
                handle.physicBody.rigidBody.isKinematic = true;
            }
            else return;

            ReleaseTelekinesis(hand.caster);
            hand.caster.ragdollHand?.playerHand?.controlHand?.HapticShort(1.5f);
        }

        private void TickFrozenList(ref HandState hand)
        {
            for (var i = hand.frozenTargets.Count - 1; i >= 0; i--)
            {
                var frozen = hand.frozenTargets[i];
                if (frozen.IsHeld())
                {
                    frozen.Release();
                    hand.frozenTargets.RemoveAt(i);
                    continue;
                }
                frozen.Hold();
                hand.frozenTargets[i] = frozen;
            }
        }

        private static void HandleRecallRelease(ref HandState hand, bool triggerHeld)
        {
            if (triggerHeld || !hand.recallActive) return;

            var ragdollHandle = hand.caster.telekinesis.catchedHandle as HandleRagdoll;
            if (ragdollHandle?.ragdollPart?.ragdoll != null)
                RestoreRagdollJoints(ragdollHandle.ragdollPart.ragdoll);
        }

        private void HandleRecallOrFloat(ref HandState hand, float triggerAxis, bool triggerHeld, bool chordHeld)
        {
            if (triggerHeld && !chordHeld && Time.time - _lastReleaseTime > RecallCooldown)
            {
                TickRecall(ref hand, triggerAxis);
                return;
            }

            if (hand.recallActive)
            {
                hand.recallTimer += Time.fixedDeltaTime;
                if (hand.recallTimer > RecallFadeTime)
                    hand.recallActive = false;
            }

            TickFloat(ref hand, triggerHeld);
        }

        private void TickRecall(ref HandState hand, float triggerAxis)
        {
            hand.recallActive = true;
            hand.recallTimer  = 0f;

            var catchedHandle = hand.caster.telekinesis.catchedHandle;
            if (catchedHandle?.physicBody == null) return;

            var handPos    = hand.caster.transform.position;
            var dirToHand  = (handPos - catchedHandle.transform.position).normalized;
            var distToHand = Vector3.Distance(handPos, catchedHandle.transform.position);
            var curved     = triggerAxis * triggerAxis * triggerAxis;

            var ragdollHandle = catchedHandle as HandleRagdoll;
            var ragdoll       = ragdollHandle?.ragdollPart?.ragdoll;

            if (ragdoll != null)
                ApplyRagdollRecall(catchedHandle, ragdoll, dirToHand, curved);
            else
                ApplyItemRecall(catchedHandle, dirToHand, curved);

            if (triggerAxis > RecallSnapTrigger && distToHand < RecallSnapDistance)
                SnapToHand(ref hand, catchedHandle, ragdollHandle, ragdoll);
            else
                UpdateRecallHaptics(hand, distToHand, triggerAxis);

            SetLine(ref hand, catchedHandle.transform.position, handPos);
        }

        private static void ApplyItemRecall(Handle handle, Vector3 dirToHand, float curved)
        {
            var speed = Mathf.Lerp(TelekinesisModSettings.RecallSpeedMin, TelekinesisModSettings.RecallSpeedMax, curved);
            ApplyVelocityOrForce(handle.physicBody, dirToHand * speed);
        }

        private static void ApplyRagdollRecall(Handle handle, Ragdoll ragdoll, Vector3 dirToHand, float curved)
        {
            foreach (var part in ragdoll.parts)
            {
                if (part?.characterJoint == null || part.characterJointLocked) continue;
                part.characterJoint.breakForce  = Mathf.Infinity;
                part.characterJoint.breakTorque = Mathf.Infinity;
            }

            var speed = Mathf.Lerp(TelekinesisModSettings.RagdollRecallSpeedMin, TelekinesisModSettings.RagdollRecallSpeedMax, curved);
            ApplyVelocityOrForce(handle.physicBody, dirToHand * speed);
        }

        private static void ApplyVelocityOrForce(PhysicBody body, Vector3 targetVelocity)
        {
            if (TelekinesisModSettings.RecallLerpMovement)
                body.velocity = Vector3.Lerp(
                    body.velocity, targetVelocity,
                    Time.fixedDeltaTime * TelekinesisModSettings.RecallLerpResponsiveness);
            else
                body.AddForce(
                    targetVelocity * TelekinesisModSettings.RecallLerpResponsiveness,
                    TelekinesisModSettings.GetRecallForceMode());
        }

        private void SnapToHand(ref HandState hand, Handle catchedHandle, HandleRagdoll ragdollHandle, Ragdoll ragdoll)
        {
            catchedHandle.physicBody.velocity        = Vector3.zero;
            catchedHandle.physicBody.angularVelocity = Vector3.zero;

            if (ragdoll != null)
                ZeroRagdollAndRestoreJoints(ragdoll);

            var shouldGrab = catchedHandle.gameObject.activeInHierarchy
                          && Vector3.Distance(hand.caster.ragdollHand.transform.position,
                                              catchedHandle.transform.position) < GrabRadius;

            ReleaseTelekinesis(hand.caster);

            if (hand.caster.ragdollHand != null && shouldGrab)
            {
                if (ragdollHandle != null)
                    hand.caster.ragdollHand.Grab(ragdollHandle);
                else
                    hand.caster.ragdollHand.GrabRelative(catchedHandle);
            }

            hand.caster.ragdollHand?.playerHand?.controlHand?.HapticShort(2f);
            if (hand.grip != null) hand.grip.isKinematic = false;
            _lastReleaseTime = Time.time;

            SetLine(ref hand, hand.caster.transform.position, hand.caster.transform.position);
        }

        private void TickFloat(ref HandState hand, bool triggerHeld)
        {
            if (_maxDistance != TelekinesisModSettings.MaxDistanceStatic)
                _maxDistance = Mathf.Lerp(_maxDistance, TelekinesisModSettings.MaxDistanceStatic, Time.deltaTime * 5f);

            HandleItemFlip(ref hand, triggerHeld);

            var catchedHandle = hand.caster.telekinesis.catchedHandle;
            if (catchedHandle == null) return;

            var targetPoint = CalculateFloatPoint(hand.caster);

            if (catchedHandle.item != null)
                hand.caster.telekinesis.gripDistance = Vector3.Distance(hand.caster.transform.position, targetPoint);

            var forceDir = targetPoint - catchedHandle.transform.position;
            catchedHandle.physicBody.AddForce(forceDir * TelekinesisModSettings.ForceMultiplier, ForceMode.Force);

            SetLine(ref hand, catchedHandle.transform.position, targetPoint);
        }

        private Vector3 CalculateFloatPoint(SpellCaster caster)
        {
            var bodyCenter = _neck != null ? _neck.position : caster.transform.position;
            var handOffset = caster.transform.position - bodyCenter;
            var localMax   = TelekinesisModSettings.MaxReach * TelekinesisModSettings.OverallSensitivity;

            var fwd     = handOffset.normalized;
            var right   = Vector3.Cross(fwd, Vector3.up).normalized;
            var up      = Vector3.Cross(right, fwd).normalized;

            var depth   = Vector3.Dot(handOffset, fwd)   * TelekinesisModSettings.DepthSensitivity;
            var lateral = Vector3.Dot(handOffset, right) * TelekinesisModSettings.LateralSensitivity * right
                        + Vector3.Dot(handOffset, up)    * TelekinesisModSettings.LateralSensitivity * up;

            var scaled = lateral + fwd * depth;
            return localMax > 0.01f
                ? bodyCenter + scaled * (_maxDistance / localMax)
                : caster.transform.position;
        }

        private void HandleItemFlip(ref HandState hand, bool triggerHeld)
        {
            if (hand.caster.ragdollHand?.playerHand?.controlHand == null) return;

            var currentMenu     = hand.caster.ragdollHand.playerHand.controlHand.alternateUsePressed;
            var justMenuPressed = currentMenu && !triggerHeld && !hand.wasMenu;
            hand.wasMenu        = currentMenu;

            if (!justMenuPressed) return;
            if (hand.caster.telekinesis.catchedHandle?.item == null) return;

            hand.inverted = !hand.inverted;

            var flipEuler = Vector3.zero;
            flipEuler[TelekinesisModSettings.FlipAxis] = 180f;

            hand.lockedItemRot *= Quaternion.Euler(flipEuler);
            hand.lockedHandRot  = hand.caster.ragdollHand.grip.rotation;

            hand.caster.telekinesis.catchedHandle.physicBody.angularVelocity = Vector3.zero;
        }

        private void UpdateRotation(ref HandState hand)
        {
            if (hand.grip == null) return;
            if (hand.wasFreezeChord) return;

            if (hand.caster.telekinesis.catchedHandle != null)
            {
                hand.desiredRot = CalculateGripRotation(ref hand);
                hand.rotDirty   = true;
            }
            else
            {
                hand.rotLocked = false;
            }
        }

        private void ApplyRotation(ref HandState hand)
        {
            if (!hand.rotDirty || hand.grip == null) return;

            var tkActive = hand.caster.telekinesis?.catchedHandle != null;
            if (!tkActive)
            {
                hand.grip.isKinematic = false;
                hand.rotDirty         = false;
                return;
            }

            hand.grip.isKinematic        = true;
            hand.grip.transform.rotation = hand.desiredRot;
            hand.grip.velocity           = Vector3.zero;
            hand.grip.angularVelocity    = Vector3.zero;
            hand.rotDirty                = false;
        }

        private Quaternion CalculateGripRotation(ref HandState hand)
        {
            if (hand.caster?.ragdollHand == null) return Quaternion.identity;

            var handle = hand.caster.telekinesis.catchedHandle;
            if (handle == null) return Quaternion.identity;

            var currentHandRot = hand.caster.ragdollHand.grip.rotation;

            if (!hand.rotLocked)
            {
                hand.lockedItemRot = handle.physicBody != null
                    ? handle.physicBody.transform.rotation
                    : handle.transform.rotation;
                hand.lockedHandRot = currentHandRot;
                hand.rotLocked     = true;
            }

            var handDelta = currentHandRot * Quaternion.Inverse(hand.lockedHandRot);
            var targetRot = handDelta * hand.lockedItemRot;

            if (hand.inverted)
                targetRot *= Quaternion.Euler(0f, 180f, 0f);

            return targetRot;
        }

        private void SyncLineRenderers()
        {
            if (TelekinesisModSettings.LinesActive && !_linesCreated) MakeLines();
            else if (!TelekinesisModSettings.LinesActive && _linesCreated) RemoveLines();
        }

        private void SetLine(ref HandState hand, Vector3 from, Vector3 to)
        {
            if (!TelekinesisModSettings.LinesActive || hand.line == null) return;
            _linePoints[0] = from;
            _linePoints[1] = to;
            hand.line.SetPositions(_linePoints);
        }

        private void MakeLines()
        {
            AttachLine(ref _left);
            AttachLine(ref _right);
            _linesCreated = true;
        }

        private static void AttachLine(ref HandState hand)
        {
            if (hand.caster == null) return;
            hand.line = hand.caster.gameObject.AddComponent<LineRenderer>();
            hand.line.receiveShadows = false;
            hand.line.material.color = Color.white;
            hand.line.startWidth     = 0.005f;
            hand.line.endWidth       = 0.005f;
            hand.line.startColor     = Color.white;
            hand.line.endColor       = Color.white;
        }

        private void RemoveLines()
        {
            DestroyLine(ref _left);
            DestroyLine(ref _right);
            _linesCreated = false;
        }

        private static void DestroyLine(ref HandState hand)
        {
            if (hand.line == null) return;
            Object.Destroy(hand.line);
            hand.line = null;
        }

        private static void ReleaseTelekinesis(SpellCaster caster)
        {
            caster.telekinesis.TryRelease();
            caster.telekinesis.catchedHandle = null;
            caster.telekinesis.justCatched   = false;
            caster.telekinesis.lastCatched   = null;
        }

        private static void RestoreRagdollJoints(Ragdoll ragdoll)
        {
            foreach (var part in ragdoll.parts)
                FrozenTarget.RestoreJointLimits(part);
        }

        private static void ZeroRagdollAndRestoreJoints(Ragdoll ragdoll)
        {
            foreach (var part in ragdoll.parts)
            {
                if (part?.physicBody == null) continue;
                part.physicBody.velocity        = Vector3.zero;
                part.physicBody.angularVelocity = Vector3.zero;
                FrozenTarget.RestoreJointLimits(part);
            }
        }

        private static void UpdateRecallHaptics(HandState hand, float distToHand, float triggerAxis)
        {
            if (hand.caster.ragdollHand?.playerHand == null) return;
            var closeness = 1f - Mathf.Clamp01(distToHand / 3f);
            var intensity = Mathf.Lerp(triggerAxis * 0.2f, 0.8f, closeness);
            hand.caster.ragdollHand.playerHand.controlHand.HapticShort(intensity);
        }
    }
}