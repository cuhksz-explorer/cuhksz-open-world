using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 挂在「商人」NPC 根或子物体上：玩家在范围内按指定键打开寝室商城（走 <see cref="DormMallHotkeyTController.TryOpenMallFromMerchant"/>，与按 T 打开一致）。
    /// 与 <see cref="NPCControl"/>（NPC behavior.cs）独立：静止商人可不加 NavMesh；要巡逻可把两者挂在同一角色上。
    /// </summary>
    public class DormMallMerchantNpc : MonoBehaviour
    {
        [Header("玩家与距离")]
        [Tooltip("为空时在场景中查找 PlayerMovement")]
        [SerializeField] Transform player;

        [SerializeField] float interactRange = 3.5f;

        [Tooltip("可选：用该物体位置作为交互中心（例如胸口挂点）；留空则用本物体 position")]
        [SerializeField] Transform interactOriginOverride;

        [Header("输入")]
        [SerializeField] KeyCode interactKey = KeyCode.F;

        [Header("提示（可选）")]
        [SerializeField] bool drawGizmoRange;

        void Start()
        {
            if (player == null)
            {
                var pm = FindObjectOfType<PlayerMovement>();
                if (pm != null)
                    player = pm.transform;
            }
        }

        void Update()
        {
            if (player == null)
                return;
            if (!Input.GetKeyDown(interactKey))
                return;

            Vector3 origin = interactOriginOverride != null
                ? interactOriginOverride.position
                : transform.position;
            float d = Vector3.Distance(origin, player.position);
            if (d > interactRange)
                return;

            InteractionNearestTriggerGate.Submit(interactKey, d, GetInstanceID(), () =>
            {
                if (!this || !enabled || player == null)
                    return;
                Vector3 nowOrigin = interactOriginOverride != null ? interactOriginOverride.position : transform.position;
                float nowD = Vector3.Distance(nowOrigin, player.position);
                if (nowD > interactRange)
                    return;
                if (DormMallHotkeyTController.Instance != null)
                    DormMallHotkeyTController.Instance.TryOpenMallFromMerchant();
            }, priority: 200);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmoRange)
                return;
            Vector3 origin = interactOriginOverride != null
                ? interactOriginOverride.position
                : transform.position;
            Gizmos.color = new Color(0.2f, 0.85f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(origin, interactRange);
        }
    }
}
