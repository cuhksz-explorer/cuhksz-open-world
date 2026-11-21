using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameQuestSystem
{
    /// <summary>
    /// 任务目标在其他场景时，引导至当前场景内可传送到该场景的最近入口：
    /// TeleportButton 或公交站（SpawnMoveRotateDisappearSceneSwitch）。
    /// 公交站 Canvas /「前往上园」等按钮初始隐藏时仍参与解析（仅用于地面导引，不要求可点击）。
    /// </summary>
    public static class QuestTeleportGuideResolver
    {
        static int _cachedSceneHandle = -1;
        static TeleportButton[] _cachedButtons = System.Array.Empty<TeleportButton>();
        static SpawnMoveRotateDisappearSceneSwitch[] _cachedBusStops = System.Array.Empty<SpawnMoveRotateDisappearSceneSwitch>();
        static InteractWithMouseAndMovementLock[] _cachedBusInteractors = System.Array.Empty<InteractWithMouseAndMovementLock>();

        public static void InvalidateCache()
        {
            _cachedSceneHandle = -1;
            _cachedButtons = System.Array.Empty<TeleportButton>();
            _cachedBusStops = System.Array.Empty<SpawnMoveRotateDisappearSceneSwitch>();
            _cachedBusInteractors = System.Array.Empty<InteractWithMouseAndMovementLock>();
        }

        public static bool TryGetNearestToScene(string destinationScene, Vector3 fromPosition, out Vector3 worldPosition)
        {
            worldPosition = default;
            string dest = SceneTeleportHelper.NormalizeSceneName(destinationScene);
            if (string.IsNullOrEmpty(dest))
                return false;

            EnsureCache();

            bool found = false;
            float bestDist = float.MaxValue;

            for (int i = 0; i < _cachedButtons.Length; i++)
            {
                var button = _cachedButtons[i];
                if (!IsUsableTeleportButtonForGuide(button))
                    continue;
                if (!SceneTeleportHelper.SceneNameMatches(button.targetSceneName, dest))
                    continue;

                Vector3 guidePos = button.transform.position;
                float dist = Vector3.Distance(fromPosition, guidePos);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                worldPosition = guidePos;
                found = true;
            }

            for (int i = 0; i < _cachedBusStops.Length; i++)
            {
                var busStop = _cachedBusStops[i];
                if (!IsUsableBusStopForGuide(busStop))
                    continue;
                if (!SceneTeleportHelper.SceneNameMatches(busStop.targetSceneName, dest))
                    continue;

                Vector3 guidePos = GetBusStopGuidePosition(busStop);
                float dist = Vector3.Distance(fromPosition, guidePos);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                worldPosition = guidePos;
                found = true;
            }

            for (int i = 0; i < _cachedBusInteractors.Length; i++)
            {
                var interactor = _cachedBusInteractors[i];
                if (!TryGetBusSignGuidePosition(interactor, dest, fromPosition, out Vector3 guidePos))
                    continue;

                float dist = Vector3.Distance(fromPosition, guidePos);
                if (dist >= bestDist)
                    continue;

                bestDist = dist;
                worldPosition = guidePos;
                found = true;
            }

            return found;
        }

        static void EnsureCache()
        {
            int handle = SceneManager.GetActiveScene().handle;
            if (_cachedSceneHandle == handle
                && _cachedButtons != null
                && _cachedBusStops != null
                && _cachedBusInteractors != null)
                return;

            _cachedSceneHandle = handle;
            _cachedButtons = Object.FindObjectsOfType<TeleportButton>(true);
            _cachedBusStops = Object.FindObjectsOfType<SpawnMoveRotateDisappearSceneSwitch>(true);
            _cachedBusInteractors = Object.FindObjectsOfType<InteractWithMouseAndMovementLock>(true);
        }

        static bool IsUsableTeleportButtonForGuide(TeleportButton button)
        {
            if (button == null)
                return false;
            if (string.IsNullOrWhiteSpace(button.targetSceneName))
                return false;
            return button.transform != null;
        }

        static bool IsUsableBusStopForGuide(SpawnMoveRotateDisappearSceneSwitch busStop)
        {
            if (busStop == null)
                return false;
            if (string.IsNullOrWhiteSpace(busStop.targetSceneName))
                return false;
            return HasBusStopGuidePosition(busStop);
        }

        static bool HasBusStopGuidePosition(SpawnMoveRotateDisappearSceneSwitch busStop)
        {
            if (busStop.spawnPosition.sqrMagnitude > 0.0001f)
                return true;
            return busStop.transform != null;
        }

        static Vector3 GetBusStopGuidePosition(SpawnMoveRotateDisappearSceneSwitch busStop)
        {
            if (busStop == null)
                return Vector3.zero;
            if (busStop.spawnPosition.sqrMagnitude > 0.0001f)
                return busStop.spawnPosition;
            return busStop.transform.position;
        }

        static bool TryGetBusSignGuidePosition(
            InteractWithMouseAndMovementLock interactor,
            string dest,
            Vector3 fromPosition,
            out Vector3 guidePos)
        {
            guidePos = default;
            if (interactor == null || interactor.targetCanvas == null)
                return false;
            if (!CanvasHasBusRouteToScene(interactor.targetCanvas, dest))
                return false;

            return TryGetNearestTriggerPosition(interactor, fromPosition, out guidePos);
        }

        static bool CanvasHasBusRouteToScene(Canvas canvas, string dest)
        {
            if (canvas == null)
                return false;

            var switches = canvas.GetComponentsInChildren<SpawnMoveRotateDisappearSceneSwitch>(true);
            for (int i = 0; i < switches.Length; i++)
            {
                var busStop = switches[i];
                if (busStop == null)
                    continue;
                if (!SceneTeleportHelper.SceneNameMatches(busStop.targetSceneName, dest))
                    continue;
                if (!IsUsableBusStopForGuide(busStop))
                    continue;
                return true;
            }

            return false;
        }

        static bool TryGetNearestTriggerPosition(
            InteractWithMouseAndMovementLock interactor,
            Vector3 fromPosition,
            out Vector3 guidePos)
        {
            guidePos = default;
            if (interactor == null)
                return false;

            float bestDist = float.MaxValue;
            bool found = false;

            var triggers = interactor.triggerObjects;
            if (triggers != null)
            {
                for (int i = 0; i < triggers.Length; i++)
                {
                    Transform t = triggers[i];
                    if (t == null)
                        continue;

                    float dist = Vector3.Distance(fromPosition, t.position);
                    if (dist >= bestDist)
                        continue;

                    bestDist = dist;
                    guidePos = t.position;
                    found = true;
                }
            }

            if (!found && interactor.transform != null)
            {
                guidePos = interactor.transform.position;
                found = true;
            }

            return found;
        }
    }
}
