using System.Reflection;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using FistVR;
using BepInEx.Logging;
using BepInEx.Configuration;
using Random = UnityEngine.Random;
using Sodalite.ModPanel;

namespace NGAspace
{
    [BepInPlugin("NGA.HardcoreSosigAI", "HardcoreSosigAI", "1.2.0")]
	[BepInDependency("nrgill28.Sodalite", "1.4.1")]
    [BepInProcess("h3vr.exe")]
    public class Plugin : BaseUnityPlugin
    {
		internal static ManualLogSource Log;
		// Configs fields.
		private static ConfigEntry<bool> disableFriendlyEntityAttention;
		private static ConfigEntry<bool> tuneEntityRecognitionTime;
		private static ConfigEntry<float> speedEntityRecognitionTime;
		private static ConfigEntry<bool> tuneAdsSpeed;
		private static ConfigEntry<float> speedAds;
        private static ConfigEntry<bool> tuneSuppressionWhileArmed;
		private static ConfigEntry<float> chanceSuppressedWhileArmed;
		private static ConfigEntry<bool> tuneSuppressionOverride;
		private static ConfigEntry<float> chanceSuppressionOverride;
        public void Awake()
        {
            Logger.LogInfo("Loading HardcoreSosigAI...");
			Log = Logger;			
			SetUpConfigFields();
            DoHarmonyPatches();
			Logger.LogInfo("Loaded HardcoreSosigAI Successfully!");
        }

		private void HandleSettingChanged(object sender, System.EventArgs e)
        {
            throw new System.NotImplementedException();
        }

		private void SetUpConfigFields() {
			Logger.LogInfo("Binding configs...");
			tuneEntityRecognitionTime = Config.Bind("AI.ReactionTime.EntityRecognition",
                                         "TuneEntityRecognitionTime",
                                         true,
                                         "If true, allows tuning of how fast AI recognizes entities (people & things).");
			disableFriendlyEntityAttention = Config.Bind("AI.ReactionTime.EntityRecognition",
                                         "DisableFriendlyEntityAttention",
                                         true,
                                         "If true, targets with friendly IFFs are completely ignored.");
			speedEntityRecognitionTime = Config.Bind("AI.ReactionTime.EntityRecognition",
                                         "speedEntityRecognitionTime",
                                         100f,
                                         new ConfigDescription("Float multiplier [0.01f, 100f], changes the speed of recognition. Game default 1f yeilds-> 1s recognition time. Using 100f->0.01s.", new AcceptableValueFloatRangeStep(0f, 100f, 0.5f)));
			tuneAdsSpeed = Config.Bind("AI.ReactionTime.AdsSpeed",
                                         "TuneAdsSpeed",
                                         true,
                                         "If true, allows tuning of sosig aim down sights speed.");
			speedAds = Config.Bind("AI.ReactionTime.AdsSpeed", 
                                                "SpeedAds",
                                                100f,
                                                new ConfigDescription("Float in [0.01,1000] describing speed multiplier of ADS speed. Game default: 0.5f.", new AcceptableValueFloatRangeStep(0.5f, 1000f, 10f)));
            tuneSuppressionOverride = Config.Bind("AI.Suppression",
                                                "TuneSuppressionOverride",
                                                false,
                                                "If enabled, overrides other suppression configs and tunes how sosigs react to suppression events under all circumstances.");
			chanceSuppressionOverride = Config.Bind("AI.Suppression.Override", 
                                                "ChanceSuppressionOverride",
                                                0f,
                                                new ConfigDescription("Integer in [0,100] describing the % chance any will be suppressed, overrides all other configs.", new AcceptableValueFloatRangeStep(0f, 100f, 1f)));
			tuneSuppressionWhileArmed = Config.Bind("AI.Suppression", 
                                                "TuneSuppressionWhileArmed",
                                                true,
                                                "If enabled, you can tune how sosigs react to suppression events if they're holding a gun with the fields below.");
			chanceSuppressedWhileArmed = Config.Bind("AI.Suppression.WhileArmed", 
                                                "ChanceSuppressedWhileArmed",
                                                100f,
                                                new ConfigDescription("Integer in [0,100] describing the % chance an armed sosig will be suppressed by and during a suppression event.", new AcceptableValueFloatRangeStep(0f, 100f, 1f)));
		}

        private void DoHarmonyPatches() {
			Logger.LogInfo("Loading harmony patches...");
			Harmony harmony = new Harmony("NGA.HardcoreSosigAI");
			// Declares the original and mypatch methods.
            MethodInfo original_SuppresionEvent = AccessTools.Method(typeof(Sosig), "SuppresionEvent");
            MethodInfo patch_SuppresionEvent = AccessTools.Method(typeof(Plugin), "SuppresionEvent_MyPatch");
            MethodInfo original_SuppresionUpdate = AccessTools.Method(typeof(Sosig), "SuppresionUpdate");
            MethodInfo patch_SuppresionUpdate = AccessTools.Method(typeof(Plugin), "SuppresionUpdate_MyPatch");
			MethodInfo original_StateBailCheck_ShouldISkirmish = AccessTools.Method(typeof(Sosig), "StateBailCheck_ShouldISkirmish");
            MethodInfo patch_StateBailCheck_ShouldISkirmish = AccessTools.Method(typeof(Plugin), "StateBailCheck_ShouldISkirmish_MyPatch");
			MethodInfo original_BrainUpdate_Skirmish = AccessTools.Method(typeof(Sosig), "BrainUpdate_Skirmish");
            MethodInfo patch_BrainUpdate_Skirmish = AccessTools.Method(typeof(Plugin), "BrainUpdate_Skirmish_MyPatch");
			//MethodInfo original_HandUpdate_AttackTarget = AccessTools.Method(typeof(Sosig), "HandUpdate_AttackTarget");
            //MethodInfo patch_HandUpdate_AttackTarget = AccessTools.Method(typeof(Plugin), "HandUpdate_AttackTarget_MyPatch");
			MethodInfo original_Hold = AccessTools.Method(typeof(SosigHand), "Hold");
            MethodInfo patch_Hold = AccessTools.Method(typeof(Plugin), "Hold_MyPatch");
			// Patch the methods.
            harmony.Patch(original_SuppresionEvent, new HarmonyMethod(patch_SuppresionEvent));
			harmony.Patch(original_SuppresionUpdate, new HarmonyMethod(patch_SuppresionUpdate));
			harmony.Patch(original_StateBailCheck_ShouldISkirmish, new HarmonyMethod(patch_StateBailCheck_ShouldISkirmish));
			harmony.Patch(original_BrainUpdate_Skirmish, new HarmonyMethod(patch_BrainUpdate_Skirmish));
			//harmony.Patch(original_HandUpdate_AttackTarget, new HarmonyMethod(patch_HandUpdate_AttackTarget));
			harmony.Patch(original_Hold, new HarmonyMethod(patch_Hold));
		}

		public static void Hold_MyPatch(SosigHand __instance)
		{
			if (!__instance.IsHoldingObject || __instance.HeldObject == null)
			{
				return;
			}
			if (__instance.Root == null)
			{
				return;
			}
			__instance.UpdateGunHandlingPose();
			Vector3 position = __instance.Target.position;
			Quaternion rotation = __instance.Target.rotation;
			Vector3 position2 = __instance.HeldObject.RecoilHolder.position;
			Quaternion rotation2 = __instance.HeldObject.RecoilHolder.rotation;
			if (__instance.HeldObject.O.IsHeld)
			{
				float num = Vector3.Distance(position, position2);
				if (num > 0.7f)
				{
					__instance.DropHeldObject();
					return;
				}
			}
			else
			{
				float num2 = Vector3.Distance(position, position2);
				if (num2 < 0.2f)
				{
					__instance.m_timeAwayFromTarget = 0f;
				}
				else
				{
					__instance.m_timeAwayFromTarget += Time.deltaTime;
					if (__instance.m_timeAwayFromTarget > 1f)
					{
						__instance.HeldObject.O.RootRigidbody.position = position;
						__instance.HeldObject.O.RootRigidbody.rotation = rotation;
					}
				}
			}
			if ((__instance.HeldObject.Type == SosigWeapon.SosigWeaponType.Melee || __instance.HeldObject.Type == SosigWeapon.SosigWeaponType.Grenade) && __instance.HeldObject.O.MP.IsMeleeWeapon)
			{
				Vector3 vector = __instance.Target.position - __instance.m_lastPos;
				vector *= 1f / Time.deltaTime;
				__instance.HeldObject.O.SetFakeHand(vector, __instance.Target.position);
			}
			float num3 = 0f;
			float num4 = 0f;
			float num5 = 0f;
			if (__instance.m_posedToward != null && __instance.Pose != SosigHand.SosigHandPose.Melee)
			{
				if (__instance.HasActiveAimPoint)
				{
					if (__instance.Pose == SosigHand.SosigHandPose.Aimed)
					{
						num3 = __instance.vertOffsets[__instance.m_curFiringPose_Aimed];
						num4 = __instance.forwardOffsets[__instance.m_curFiringPose_Aimed];
						num5 = __instance.tiltLerpOffsets[__instance.m_curFiringPose_Aimed];
					}
					else if (__instance.Pose == SosigHand.SosigHandPose.HipFire)
					{
						num3 = __instance.vertOffsets[__instance.m_curFiringPose_Hip];
						num4 = __instance.forwardOffsets[__instance.m_curFiringPose_Hip];
						num5 = __instance.tiltLerpOffsets[__instance.m_curFiringPose_Hip];
					}
				}
				Transform transform = __instance.S.Links[1].transform;
				float num6 = 4f;
				if (__instance.S.IsFrozen)
				{
					num6 = 0.25f;
				}
				if (__instance.S.IsSpeedUp)
				{
					num6 = 8f;
				}
				__instance.Target.position = Vector3.Lerp(position, __instance.m_posedToward.position + transform.up * num3 + __instance.m_posedToward.forward * num4, Time.deltaTime * num6);
				__instance.Target.rotation = Quaternion.Slerp(rotation, __instance.m_posedToward.rotation, Time.deltaTime * num6);
			}
			Vector3 vector2 = position2;
			Quaternion quaternion = rotation2;
			Vector3 vector3 = position;
			Quaternion quaternion2 = rotation;
			if (__instance.HasActiveAimPoint && (__instance.Pose == SosigHand.SosigHandPose.HipFire || __instance.Pose == SosigHand.SosigHandPose.Aimed))
			{
				float num7 = 0f;
				float num8 = 0f;
				if (__instance.Pose == SosigHand.SosigHandPose.HipFire)
				{
					num7 = __instance.HeldObject.Hipfire_HorizontalLimit;
					num8 = __instance.HeldObject.Hipfire_VerticalLimit;
				}
				if (__instance.Pose == SosigHand.SosigHandPose.Aimed)
				{
					num7 = __instance.HeldObject.Aim_HorizontalLimit;
					num8 = __instance.HeldObject.Aim_VerticalLimit;
				}
				Vector3 vector4 = __instance.m_aimTowardPoint - position;
				Vector3 forward = __instance.Target.forward;
				Vector3 vector5 = Vector3.RotateTowards(forward, Vector3.ProjectOnPlane(vector4, __instance.Target.right), num8 * 0.0174533f, 0f);
				Vector3 vector6 = Vector3.RotateTowards(vector5, vector4, num7 * 0.0174533f, 0f);
				if (num5 > 0f)
				{
					Vector3 localPosition = __instance.Target.transform.localPosition;
					localPosition.z = 0f;
					localPosition.y = 0f;
					localPosition.Normalize();
					Vector3 vector7 = Vector3.Slerp(__instance.Target.up, localPosition.x * -__instance.Target.right, num5);
					quaternion2 = Quaternion.LookRotation(vector6, vector7);
				}
				else
				{
					quaternion2 = Quaternion.LookRotation(vector6, __instance.Target.up);
				}
			}
			Vector3 vector8 = vector3 - vector2;
			Quaternion quaternion3 = quaternion2 * Quaternion.Inverse(quaternion);
			float deltaTime = Time.deltaTime;
			float num9;
			Vector3 vector9;
			quaternion3.ToAngleAxis(out num9, out vector9);
			float num10 = 0.5f;
			if (__instance.S.IsConfused)
			{
				num10 = 0.1f;
			}
			if (__instance.S.IsStunned || __instance.S.IsUnconscious)
			{
				num10 = 0.02f;
			}
			if (num9 > 180f)
			{
				num9 -= 360f;
			}
			// NOTE: Sets custom velocity here.
			float custom_speed_mult = 0.5f;
			if (tuneAdsSpeed.Value) {
				custom_speed_mult = speedAds.Value;
			}
			if (num9 != 0f)
			{
				// NOTE: Tunes angular velocity here.
				Vector3 vector10 = deltaTime * num9 * vector9 * __instance.S.AttachedRotationMultiplier * __instance.HeldObject.PosRotMult * num10;
				__instance.HeldObject.O.RootRigidbody.angularVelocity = Vector3.MoveTowards(__instance.HeldObject.O.RootRigidbody.angularVelocity, vector10, __instance.S.AttachedRotationFudge * custom_speed_mult * Time.fixedDeltaTime);
			}
			// NOTE: Tunes velocity here.
			Vector3 vector11 = vector8 * __instance.S.AttachedPositionMultiplier * 0.5f * __instance.HeldObject.PosStrengthMult * deltaTime;
			__instance.HeldObject.O.RootRigidbody.velocity = Vector3.MoveTowards(__instance.HeldObject.O.RootRigidbody.velocity, vector11, __instance.S.AttachedPositionFudge * custom_speed_mult * deltaTime);
			__instance.m_lastPos = __instance.Target.position;
		}
		
		public static void BrainUpdate_Skirmish_MyPatch(Sosig __instance)
		{
			if (__instance.m_hasPriority)
			{
				__instance.Priority.Compute(__instance.m_suppressionLevel, 1f);
			}
			if (__instance.StateBailCheck_Equipment())
			{
				return;
			}
			__instance.WeaponEquipCycle();
			__instance.EquipmentScanCycle(new Vector3(__instance.EquipmentPickupDistance, 3f, __instance.EquipmentPickupDistance), 1.5f);
			if (!__instance.Priority.HasFreshTarget())
			{
				__instance.SetCurrentOrder(Sosig.SosigOrder.Investigate);
				return;
			}
			bool flag = __instance.DoIHaveAGun();
			bool flag2 = __instance.DoIHaveAWeaponInMyHand();
			bool flag3 = __instance.AmIReloading();
			bool flag4 = true;
			Vector3 vector = __instance.m_skirmishPoint;
			if (__instance.FallbackOrder == Sosig.SosigOrder.Assault)
			{
				float num = Vector3.Distance(__instance.Priority.GetTargetPoint(), __instance.Agent.transform.position);
				bool flag5 = false;
				if (flag && __instance.AmIOutOfRange(num))
				{
					flag5 = true;
				}
				if (num > __instance.m_assaultPointOverridesSkirmishPointWhenFurtherThan || flag5)
				{
					vector = __instance.m_assaultPoint;
					flag4 = false;
				}
			}
			else if (__instance.FallbackOrder == Sosig.SosigOrder.PathTo)
			{
				float num2 = Vector3.Distance(__instance.Priority.GetTargetPoint(), __instance.Agent.transform.position);
				bool flag6 = false;
				if (flag && __instance.AmIOutOfRange(num2))
				{
					flag6 = true;
				}
				if (num2 > __instance.m_pathToPointOverridesSkirmishPointWhenFurtherThan || flag6)
				{
					vector = __instance.m_pathToPoint;
					flag4 = false;
				}
			}
			if (flag4)
			{
				__instance.ShouldIPickANewSkirmishPoint(flag, flag3);
				vector = __instance.m_skirmishPoint;
			}
			__instance.TryToGetTo(vector);
			__instance.m_faceTowards = __instance.Priority.GetTargetPoint() - __instance.Agent.transform.position;
			__instance.m_faceTowards.y = 0f;
			__instance.SetHandObjectUsage(Sosig.SosigObjectUsageFocus.AttackTarget);
			__instance.SetMovementState(Sosig.SosigMovementState.MoveToPoint);
			if (flag)
			{
				if (__instance.m_suppressionLevel > 0.2f && !__instance.Agent.isOnOffMeshLink)
				{
					if (__instance.m_curCoverPoint != null)
					{
						float num3 = Vector3.Distance(__instance.m_curCoverPoint.Pos, __instance.transform.position);
						if (num3 > 2f)
						{
							__instance.SetMovementSpeed(Sosig.SosigMoveSpeed.Running);
						}
						else
						{
							__instance.SetMovementSpeed(Sosig.SosigMoveSpeed.Walking);
						}
					}
					else
					{
						__instance.SetMovementSpeed(Sosig.SosigMoveSpeed.Sneaking);
					}
				}
				else if (flag3)
				{
					__instance.SetMovementSpeed(Sosig.SosigMoveSpeed.Walking);
				}
				else
				{
					__instance.SetMovementSpeed(Sosig.SosigMoveSpeed.Running);
				}
			}
			else
			{
				__instance.SetMovementSpeed(Sosig.SosigMoveSpeed.Running);
			}
			if (__instance.CanSpeakState())
			{
				if (flag3 && __instance.Speech.OnReloading.Count > 0)
				{
					__instance.Speak_State(__instance.Speech.OnReloading);
				}
				else if ((__instance.IsHealing || __instance.IsInvuln) && __instance.Speech.OnMedic.Count > 0)
				{
					__instance.Speak_State(__instance.Speech.OnMedic);
				}
				else
				{
					__instance.Speak_State(__instance.Speech.OnSkirmish);
				}
			}
			if (__instance.HasABrain && !__instance.Agent.isOnOffMeshLink && (__instance.m_suppressionLevel > 0.2f || !flag2 || flag3))
			{
				if (flag3 || !flag2 || __instance.m_suppressionLevel > 0.8f)
				{
					__instance.SetBodyPose(Sosig.SosigBodyPose.Prone);
				}
				else
				{
					__instance.SetBodyPose(Sosig.SosigBodyPose.Crouching);
				}
			}
			else if (__instance.Agent.velocity.magnitude > 0.4f)
			{
				__instance.SetBodyPose(Sosig.SosigBodyPose.Standing);
			}
			else
			{
				__instance.SetBodyPose(Sosig.SosigBodyPose.Crouching);
			}
		}

        public static void SuppresionEvent_MyPatch(Sosig __instance, Vector3 pos, Vector3 dir, int IFF, float intensity, float range)
        {
			if (tuneSuppressionOverride.Value) {
				float percentage = (float)Mathf.Clamp(chanceSuppressionOverride.Value, 0, 100) / 100f;
				if (Random.value <= percentage) {
					__instance.m_suppressionLevel = 0f;
					return;
				}
			}
			if (__instance.CurrentOrder == Sosig.SosigOrder.Disabled)
			{
				return;
			}
			if (!__instance.HasABrain)
			{
				return;
			}
			if (__instance.m_isUnconscious)
			{
				return;
			}
			if (!__instance.CanBeSuppresed)
			{
				return;
			}
			if (__instance.m_isInvuln)
			{
				return;
			}
			if (__instance.m_isDamResist)
			{
				return;
			}
			if (__instance.DoIHaveAShieldInMyHand())
			{
				return;
			}
            if (tuneSuppressionWhileArmed.Value && __instance.DoIHaveAWeaponAtAll())
			{
				float percentage = (float)Mathf.Clamp(chanceSuppressedWhileArmed.Value, 0, 100) / 100f;
				if (Random.value <= percentage) {
					__instance.m_suppressionLevel = 0f;
					return;
				}
			}
			if ((IFF < 0 || IFF == __instance.E.IFFCode) & __instance.DoIHaveAWeaponAtAll())
			{
				return;
			}
			__instance.m_lastSuppresionEventPoint = pos;
			if (__instance.m_suppressionLevel >= 1f)
			{
				return;
			}
			float num = Vector3.Distance(pos, __instance.transform.position);
			num = Mathf.Clamp(num - 1f, 0f, range);
			float num2 = intensity * ((range - num) / range);
			if (num2 > 0f)
			{
				dir.y = 0f;
				dir.z += 0.0001f;
				dir.Normalize();
				__instance.m_suppressionDir = Vector3.Lerp(__instance.m_suppressionDir, dir, 0.5f);
				__instance.m_suppressionDir.Normalize();
				__instance.m_suppressionLevel += Mathf.Clamp(num2, 0f, 1f) * __instance.SuppressionMult;
			}
		}

        public static void SuppresionUpdate_MyPatch(Sosig __instance)
		{
			if (__instance.m_isInvuln || __instance.m_isDamResist)
			{
				__instance.m_suppressionLevel = 0f;
			}
            if (__instance.DoIHaveAWeaponAtAll())
			{
				__instance.m_suppressionLevel = 0f;
                return;
			}
			if (__instance.m_suppressionLevel > 0f && __instance.BodyState == Sosig.SosigBodyState.InControl)
			{
				__instance.m_suppressionLevel -= Time.deltaTime * 0.25f;
			}
			if (__instance.m_suppressionLevel > 0f && __instance.BodyState == Sosig.SosigBodyState.Controlled)
			{
				__instance.m_suppressionLevel -= Time.deltaTime * 1f;
			}
		}

		public static bool StateBailCheck_ShouldISkirmish_MyPatch(Sosig __instance) 
		{
			if (__instance.m_isBlinded)
			{
				return false;
			}
			if (__instance.Priority.HasFreshTarget())
			{
				if (__instance.Priority.IsTargetEntity() && !__instance.Priority.GetTargetEntity().IsPassiveEntity)
				{
					if (!(disableFriendlyEntityAttention.Value && __instance.Priority.GetTargetEntity().IFFCode == __instance.GetIFF())) {
						// Changed num to 1, so enemy recognition is >hopefully< faster even if not already investigating.
						float num = 1f;
						if (__instance.CurrentOrder == Sosig.SosigOrder.Investigate)
						{
							num = 1f;
						}
						if (__instance.m_entityRecognition < 1f)
						{
							float speedModifier = 1f;
							if (tuneEntityRecognitionTime.Value) {
								speedModifier = Mathf.Clamp(speedEntityRecognitionTime.Value, 0.01f, 100f);
							}
							__instance.m_entityRecognition += Time.deltaTime * num * speedModifier;
						}
						if (__instance.m_entityRecognition >= 1f)
						{
							__instance.SetCurrentOrder(Sosig.SosigOrder.Skirmish);
							return true;
						}
					}
				}
				else if (__instance.m_entityRecognition > 0f)
				{
					__instance.m_entityRecognition -= Time.deltaTime;
				}
				if (__instance.CurrentOrder != Sosig.SosigOrder.Investigate && __instance.m_alertnessLevel >= 1f)
				{
					if (__instance.Priority.GetTargetEntity() != null && __instance.Priority.GetTargetEntity().IsPassiveEntity)
					{
					 	__instance.Priority.DisregardEntity(__instance.Priority.GetTargetEntity());
					}
					__instance.SetCurrentOrder(Sosig.SosigOrder.Investigate);
					__instance.m_investigateCooldown = UnityEngine.Random.Range(8f, 11f);
					return true;
				}
			}
			else if (__instance.m_entityRecognition > 0f)
			{
				__instance.m_entityRecognition -= Time.deltaTime;
			}
			return false;
		}
    }
}
