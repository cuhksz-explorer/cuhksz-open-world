using System.Collections;
using GameQuestSystem;
using Mirror;
using UnityEngine;

namespace CombatSystem
{
    [DisallowMultipleComponent]
    public class WeaponController : MonoBehaviour
    {
        PlayerMovement _movement;
        CameraSwitcher _cameraSwitcher;
        FirstPersonCamera _fpsCamera;
        WeaponDatabase _database;
        CoopCombatRelay _coopRelay;

        int _consecutiveShots;
        float _timeSinceLastShot;
        float _nextFireTime;
        float _recoilPitch;
        float _prevHorizontalSpeed;
        float _recoilRecoverTimer;
        Coroutine _reloadCoroutine;

        public static WeaponController Local { get; private set; }
        public static bool IsReloading { get; private set; }

        void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _database = WeaponDatabase.Load();
            WeaponInventoryData.EnsureInitialized();
        }

        void OnEnable()
        {
            if (_movement != null && _movement == FindObjectOfType<PlayerMovement>())
                Local = this;
            WeaponInventoryData.OnChanged += OnInventoryChanged;
        }

        void OnDisable()
        {
            WeaponInventoryData.OnChanged -= OnInventoryChanged;
            CancelReload();
            if (Local == this)
                Local = null;
        }

        void Start()
        {
            _cameraSwitcher = FindObjectOfType<CameraSwitcher>();
            _fpsCamera = FindObjectOfType<FirstPersonCamera>(true);
            TryBindCoopRelay();
            EnsureEquippedAmmo();
        }

        void TryBindCoopRelay()
        {
            if (!NetworkClient.active)
                return;
            foreach (var cp in FindObjectsOfType<CoopNetworkPlayer>())
            {
                if (cp.isLocalPlayer)
                {
                    _coopRelay = cp.GetComponent<CoopCombatRelay>();
                    break;
                }
            }
        }

        void OnInventoryChanged()
        {
            _consecutiveShots = 0;
            _recoilPitch = 0f;
            CancelReload();
            EnsureEquippedAmmo();
        }

        void EnsureEquippedAmmo()
        {
            var weapon = GetEquippedWeapon();
            if (weapon != null)
                WeaponAmmoState.EnsureWeapon(weapon.id, weapon);
        }

        void Update()
        {
            if (_movement == null || _movement.IsDeathReviveFlowActive)
                return;

            if (!CanUseCombatInput())
                return;

            _timeSinceLastShot += Time.deltaTime;
            if (_timeSinceLastShot > 0.3f)
                _consecutiveShots = 0;

            if (_recoilRecoverTimer > 0f)
            {
                _recoilRecoverTimer -= Time.deltaTime;
                if (_recoilRecoverTimer <= 0f)
                    _recoilPitch = 0f;
            }

            HandleWeaponSwitchInput();

            if (IsReloading)
                return;

            if (CombatHostBridge.WasKeyDown(CombatHostBridge.ReloadKey))
                TryStartReload();

            if (Input.GetMouseButton(0))
                TryFire();
        }

        void HandleWeaponSwitchInput()
        {
            if (IsReloading)
                return;

            if (CombatHostBridge.WasKeyDown(CombatHostBridge.QuickSwitchKey))
                WeaponInventoryData.TryQuickSwitch();

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.01f)
                WeaponInventoryData.TrySwitchPrevious();
            else if (scroll < -0.01f)
                WeaponInventoryData.TrySwitchNext();
        }

        bool CanUseCombatInput()
        {
            if (!CombatHudController.ShouldShowCombatHud())
                return false;
            if (_cameraSwitcher != null && !_cameraSwitcher.IsFirstPersonActive)
                return false;
            if (_fpsCamera == null || !_fpsCamera.IsActive)
                return false;
            if (GameplayUiHotkeyGuard.ShouldBlockGameplayHotkeys())
                return false;
            if (_movement != null && !_movement.enabled)
                return false;
            return true;
        }

        void TryStartReload()
        {
            var weapon = GetEquippedWeapon();
            if (weapon == null)
                return;
            if (!WeaponAmmoState.CanReload(weapon.id, weapon))
                return;
            if (_reloadCoroutine != null)
                return;

            _reloadCoroutine = StartCoroutine(ReloadRoutine(weapon));
        }

        IEnumerator ReloadRoutine(WeaponEntry weapon)
        {
            IsReloading = true;
            float duration = WeaponStatsHelper.GetReloadTime(weapon);
            yield return new WaitForSeconds(duration);

            if (weapon.id == WeaponInventoryData.EquippedWeaponId)
                WeaponAmmoState.TryReload(weapon.id, weapon);

            IsReloading = false;
            _reloadCoroutine = null;
        }

        void CancelReload()
        {
            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
                _reloadCoroutine = null;
            }
            IsReloading = false;
        }

        void TryFire()
        {
            var weapon = GetEquippedWeapon();
            if (weapon == null)
                return;

            if (Time.time < _nextFireTime)
                return;

            WeaponAmmoState.EnsureWeapon(weapon.id, weapon);
            if (!WeaponAmmoState.HasAmmoInMagazine(weapon.id))
            {
                TryStartReload();
                return;
            }

            if (!WeaponAmmoState.TryConsumeRound(weapon.id, weapon))
                return;

            _nextFireTime = Time.time + WeaponSpreadModel.ComputeFireInterval(weapon);

            var cam = Camera.main;
            if (cam == null)
                return;

            float horizontalSpeed = new Vector3(_movement.horizontalInput, 0f, _movement.verticalInput).magnitude
                                    * (_movement.isCurrentlyRunning ? _movement.runSpeedMultiplier : 1f);

            var spreadInput = new WeaponSpreadModel.SpreadInput
            {
                penStyle = weapon.penStyle,
                isGrounded = true,
                isRunning = _movement.isCurrentlyRunning,
                isMoving = horizontalSpeed > 0.1f,
                horizontalSpeed = horizontalSpeed,
                prevHorizontalSpeed = _prevHorizontalSpeed,
                consecutiveShots = _consecutiveShots,
                timeSinceLastShot = _timeSinceLastShot
            };

            float spread = WeaponSpreadModel.ComputeSpreadDegrees(in spreadInput, weapon);
            Vector3 dir = WeaponSpreadModel.ApplySpread(cam.transform.forward, spread);
            float range = WeaponSpreadModel.ComputeEffectiveRange(weapon);
            Vector3 origin = cam.transform.position;

            _consecutiveShots++;
            _timeSinceLastShot = 0f;
            _prevHorizontalSpeed = horizontalSpeed;

            float kick = WeaponSpreadModel.ComputeRecoilKick(weapon.penStyle);
            _recoilPitch += kick;
            _recoilRecoverTimer = 0.3f;
            ApplyRecoil(kick);

            Vector3 endPoint = origin + dir * range;
            if (Physics.Raycast(origin, dir, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
                endPoint = hit.point;

            Color tracerColor = weapon.RarityColor;
            BulletTracerPool.Instance?.Play(origin, endPoint, tracerColor);

            if (Physics.Raycast(origin, dir, out hit, range, ~0, QueryTriggerInteraction.Ignore))
                ResolveHit(hit, weapon);

            if (_coopRelay == null)
                TryBindCoopRelay();

            if (!WeaponAmmoState.HasAmmoInMagazine(weapon.id) && WeaponAmmoState.CanReload(weapon.id, weapon))
                TryStartReload();
        }

        void ApplyRecoil(float kick)
        {
            if (_fpsCamera == null)
                return;
            var holder = _fpsCamera.cameraHolder != null ? _fpsCamera.cameraHolder : _fpsCamera.transform;
            if (holder == null)
                return;
            var e = holder.localEulerAngles;
            e.x = Mathf.Clamp(NormalizePitch(e.x) - kick, -60f, 85f);
            holder.localEulerAngles = e;
        }

        static float NormalizePitch(float x)
        {
            if (x > 180f) x -= 360f;
            return x;
        }

        void ResolveHit(RaycastHit hit, WeaponEntry weapon)
        {
            if (CombatHitUtility.IsProtectedNpc(hit.collider.gameObject))
                return;

            var remote = CombatHitUtility.FindRemotePlayerHitbox(hit.collider);
            if (remote != null && !remote.IsLocalOwnedPlayer())
            {
                if (_coopRelay != null)
                {
                    uint netId = remote.Owner != null ? remote.Owner.netId : 0;
                    if (netId != 0)
                        _coopRelay.CmdReportHit(netId, weapon.id, weapon.damage, transform.position);
                }
                return;
            }

            var damageable = CombatHitUtility.FindDamageable(hit.collider);
            if (damageable != null && damageable.IsAlive)
                damageable.TakeCombatDamage(weapon.damage, gameObject);
        }

        WeaponEntry GetEquippedWeapon()
        {
            if (_database == null)
                _database = WeaponDatabase.Load();
            return _database != null ? _database.GetById(WeaponInventoryData.EquippedWeaponId) : null;
        }

        public static bool TryEquipWeapon(int weaponId)
        {
            if (!WeaponInventoryData.TryEquip(weaponId))
                return false;
            return true;
        }
    }
}
