using UnityEngine;

namespace PhoneUI
{
    public class PhonePlayerRigSetup : MonoBehaviour
    {
        public PhonePresentationController presentation;
        public PlayerMovement playerMovement;
        public Vector3 anchorLocalPosition = new Vector3(0f, 1.45f, 1.85f);
        public Vector3 propLocalPosition = new Vector3(0.02f, 0.02f, 0.05f);
        public Vector3 propLocalEuler = new Vector3(0f, 90f, 0f);
        public Vector3 propLocalScale = new Vector3(0.01f, 0.01f, 0.01f);
        [Tooltip("优先将 PhoneProp 挂到右手骨骼；找不到则挂在玩家根下。")]
        public bool attachPropToRightHand = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoWireAfterSceneLoad()
        {
            var session = PhoneSessionController.FindBestInScene();
            if (session == null || !PhoneSessionController.IsConfigured(session))
                return;

            PhoneSessionController.ClaimInstance(session);
            if (session.presentation == null)
                session.presentation = Object.FindObjectOfType<PhonePresentationController>(true);

            if (session.presentation != null)
                session.launcherOnly = false;

            var setup = session.GetComponent<PhonePlayerRigSetup>();
            if (setup == null)
                setup = session.gameObject.AddComponent<PhonePlayerRigSetup>();
            setup.presentation = session.presentation;
            setup.playerMovement = session.playerMovement;
            setup.EnsureRig();
        }

        void Start()
        {
            if (presentation == null)
            {
                var session = GetComponent<PhoneSessionController>();
                if (session != null)
                    presentation = session.presentation;
            }

            if (presentation == null)
                return;

            EnsureRig();
        }

        public void EnsureRig()
        {
            var session = GetComponent<PhoneSessionController>();
            if (session == null || !PhoneSessionController.IsConfigured(session))
                session = PhoneSessionController.FindBestInScene();

            if (presentation == null && session != null)
                presentation = session.presentation;
            if (presentation == null)
                presentation = GetComponent<PhonePresentationController>();
            if (presentation == null)
                return;

            if (session != null && session.presentation == null)
                session.presentation = presentation;

            if (playerMovement == null && session != null)
                playerMovement = session.playerMovement;

            if (playerMovement == null && presentation.playerTransform != null)
                playerMovement = presentation.playerTransform.GetComponent<PlayerMovement>();

            Transform player = playerMovement != null ? playerMovement.transform : null;
            if (player == null)
            {
                var pm = Object.FindObjectOfType<PlayerMovement>();
                if (pm != null)
                    player = pm.transform;
            }

            if (player == null)
                return;

            presentation.playerTransform = player;
            presentation.phoneViewTarget = player;

            var anchorChild = player.Find("PhoneViewAnchor");
            if (anchorChild != null)
                presentation.phoneViewAnchor = anchorChild;
            else if (presentation.phoneViewAnchor == null
                     || presentation.phoneViewAnchor == presentation.playerTransform)
            {
                var anchorT = new GameObject("PhoneViewAnchor").transform;
                anchorT.SetParent(player, false);
                anchorT.localPosition = anchorLocalPosition;
                anchorT.localRotation = Quaternion.identity;
                presentation.phoneViewAnchor = anchorT;
            }

            if (presentation.cameraSwitcher == null)
                presentation.cameraSwitcher = player.GetComponentInChildren<CameraSwitcher>(true);

            if (presentation.thirdPersonCamera == null && presentation.cameraSwitcher != null)
                presentation.thirdPersonCamera = presentation.cameraSwitcher.thirdPersonCamera;

            if (presentation.animator == null)
                presentation.animator = player.GetComponentInChildren<Animator>(true);

            EnsurePhoneProp(player);
        }

        void EnsurePhoneProp(Transform player)
        {
            if (presentation.phoneProp != null
                && presentation.phoneProp != player.gameObject
                && !IsWrongRootBinding(presentation.phoneProp, player))
                return;

            Transform found = null;
            var all = player.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "PhoneProp")
                {
                    found = all[i];
                    break;
                }
            }

            if (found != null)
            {
                presentation.phoneProp = found.gameObject;
                presentation.phoneProp.SetActive(false);
                return;
            }

            var prefab = Resources.Load<GameObject>("PhoneUI/PhoneProp");
            Transform parent = player;
            if (attachPropToRightHand && presentation.animator != null && presentation.animator.isHuman)
            {
                var hand = presentation.animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                    parent = hand;
            }

            GameObject propGo;
            if (prefab != null)
            {
                propGo = Object.Instantiate(prefab, parent, false);
                propGo.name = "PhoneProp";
            }
            else
            {
                propGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                propGo.name = "PhoneProp";
                propGo.transform.SetParent(parent, false);
                propGo.transform.localScale = new Vector3(0.06f, 0.12f, 0.02f);
                var col = propGo.GetComponent<Collider>();
                if (col != null)
                    Object.Destroy(col);
            }

            propGo.transform.localPosition = propLocalPosition;
            propGo.transform.localEulerAngles = propLocalEuler;
            if (parent != player)
                propGo.transform.localScale = propLocalScale;

            propGo.SetActive(false);
            presentation.phoneProp = propGo;
        }

        static bool IsWrongRootBinding(GameObject prop, Transform player)
        {
            return prop == player.gameObject;
        }
    }
}
