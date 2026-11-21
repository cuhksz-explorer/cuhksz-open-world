using System;

using System.Threading.Tasks;

using UnityEngine;

using UnityEngine.UI;



namespace DormMall

{

    /// <summary>

    /// 已合并到 <see cref="TeleportButton"/>：将 <c>dormRole</c> 设为 <see cref="DormTeleportRole.ExitDorm"/> 即可。

    /// 保留本组件仅为旧场景兼容；若同物体已有 TeleportButton，会在 Awake 时同步兜底并禁用本脚本。

    /// </summary>

    [Obsolete("请改用 TeleportButton，在 Inspector 将 dormRole 设为 ExitDorm。")]

    [RequireComponent(typeof(Button))]

    public class DormExitTeleportButton : MonoBehaviour

    {

        [Header("（已废弃，请改 TeleportButton）")]

        public string fallbackSceneName = DormSceneConfig.DefaultFallbackReturnScene;

        public Vector3 fallbackPosition = DormSceneConfig.DefaultFallbackReturnPosition;

        public Vector3 fallbackRotation = DormSceneConfig.DefaultFallbackReturnRotation;



        Button _button;



        void Awake()

        {

            var teleport = GetComponent<TeleportButton>();

            if (teleport != null)

            {

                teleport.dormRole = DormTeleportRole.ExitDorm;

                teleport.dormExitFallbackSceneName = fallbackSceneName;

                teleport.dormExitFallbackPosition = fallbackPosition;

                teleport.dormExitFallbackRotation = fallbackRotation;

                enabled = false;

                return;

            }



            _button = GetComponent<Button>();

            _button.onClick.AddListener(OnLegacyClick);

        }



        async void OnLegacyClick()

        {

            Debug.LogWarning(

                "[DormExitTeleport] 请为本按钮添加 TeleportButton，并将 dormRole 设为 ExitDorm。", this);

            await Task.CompletedTask;

        }



        public Task RunExitDormAsync()

        {

            var teleport = GetComponent<TeleportButton>();

            if (teleport == null)

            {

                Debug.LogError("[DormExitTeleport] 需要同物体上的 TeleportButton。", this);

                return Task.CompletedTask;

            }



            return teleport.RunExitDormAsync();

        }

    }

}


