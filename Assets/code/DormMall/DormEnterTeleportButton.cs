using System;

using UnityEngine;



namespace DormMall

{

    /// <summary>

    /// 已合并到 <see cref="TeleportButton"/>：将 <c>dormRole</c> 设为 <see cref="DormTeleportRole.EnterDorm"/> 即可。

    /// 保留本组件仅为旧场景兼容；若同物体已有 TeleportButton，会在 Awake 时同步设置并禁用本脚本。

    /// </summary>

    [Obsolete("请改用 TeleportButton，在 Inspector 将 dormRole 设为 EnterDorm。")]

    public class DormEnterTeleportButton : MonoBehaviour

    {

        [Header("（已废弃，请改 TeleportButton）")]

        public string dormSceneName = DormSceneConfig.UnitySceneName;

        public Vector3 dormSpawnPosition;

        public Vector3 dormSpawnRotation;

        public DormReturnAnchor returnAnchor;



        void Awake()

        {

            var teleport = GetComponent<TeleportButton>();

            if (teleport == null)

                return;



            teleport.dormRole = DormTeleportRole.EnterDorm;

            if (!string.IsNullOrWhiteSpace(dormSceneName))

                teleport.targetSceneName = dormSceneName;

            teleport.targetPosition = dormSpawnPosition;

            teleport.targetRotation = dormSpawnRotation;

            if (returnAnchor != null)

                teleport.dormReturnAnchor = returnAnchor;

            enabled = false;

        }



        /// <summary>转发至 <see cref="TeleportButton.RecordDormReturnPoint(MonoBehaviour)"/>。</summary>

        public static void RecordReturnPointForTeleportButton(MonoBehaviour host) =>

            TeleportButton.RecordDormReturnPoint(host);

    }

}


