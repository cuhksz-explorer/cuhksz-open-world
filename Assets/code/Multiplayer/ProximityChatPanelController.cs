using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror;

namespace Multiplayer
{
    [DisallowMultipleComponent]
    public class ProximityChatPanelController : MonoBehaviour
    {
        [SerializeField] KeyCode openKey = KeyCode.F;
        [SerializeField] float interactDistance = 3.2f;
        [SerializeField] bool autoBuildUi = true;
        [SerializeField] bool useFriendSidebarFlow = true;
        [SerializeField] bool useSharedPromptPanel = true;
        [SerializeField] float autoFriendRequestCooldownSeconds = 8f;

        static ProximityChatPanelController s_instance;
        MultiplayerApiClient _api;
        CoopNetworkPlayer _local;
        CoopNetworkPlayer _target;
        int _lastMsgId;
        Coroutine _poll;
        Coroutine _friendRefresh;
        bool _handlingInteract;
        readonly HashSet<int> _friendIds = new HashSet<int>();
        TextMeshProUGUI _sharedPromptNameTmp;
        TextMeshProUGUI _sharedPromptTypeTmp;
        int _sharedOfferGroupId;
        InteractWithMouseAndMovementLock _mode2Interactor;
        readonly Dictionary<int, float> _lastAutoFriendRequestAt = new Dictionary<int, float>();
        readonly Dictionary<int, string> _friendDisplayById = new Dictionary<int, string>();

        GameObject _panelRoot;
        TMP_Text _title;
        TMP_Text _log;
        TMP_InputField _input;
        public bool IsStandalonePanelOpen => _panelRoot != null && _panelRoot.activeInHierarchy;

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                enabled = false;
                return;
            }
            s_instance = this;
            _api = GetComponent<MultiplayerApiClient>() ?? gameObject.AddComponent<MultiplayerApiClient>();
            if (autoBuildUi)
                BuildUi();
            ResolveSharedPromptRefs();
        }

        void OnEnable()
        {
            if (_friendRefresh != null) StopCoroutine(_friendRefresh);
            _friendRefresh = StartCoroutine(CoRefreshFriendIdsLoop());
        }

        void OnDisable()
        {
            if (_friendRefresh != null) StopCoroutine(_friendRefresh);
            _friendRefresh = null;
            if (s_instance == this)
                s_instance = null;
        }

        void Update()
        {
            if (_api == null || !_api.IsReady || !CoopSessionState.InSession)
                return;
            if (!useFriendSidebarFlow && _panelRoot == null)
                return;

            if (_local == null)
                _local = FindLocalCoop();
            if (_local == null)
                return;
            if (useSharedPromptPanel && _sharedOfferGroupId == 0)
                ResolveSharedPromptRefs();

            if (!useFriendSidebarFlow && _panelRoot != null && _panelRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    Close();
                return;
            }

            _target = FindClosestPeer(_local.transform.position);
            if (_target == null || _target.userId <= 0)
                return;

            float d = Vector3.Distance(_local.transform.position, _target.transform.position);
            int gid = _sharedOfferGroupId;
            if (gid == 0 && _title != null)
                gid = _title.GetInstanceID();
            bool isFriendCached = _friendIds.Contains(_target.userId);
            string action = isFriendCached ? Loc.Choose("聊天", "Chat") : Loc.Choose("添加好友", "Add friend");
            string label = BuildTargetPromptLabel(_target);
            InteractPromptSharedLabelFrame.Offer(gid, d, label, $"{action} ({openKey})", _target.transform.position);
            if (d <= interactDistance && Input.GetKeyDown(openKey) && !_handlingInteract)
            {
                var targetSnapshot = _target;
                float claimD = d;
                InteractionNearestTriggerGate.Submit(openKey, claimD, GetInstanceID(), () =>
                {
                    if (!this || !enabled || _handlingInteract || _local == null || targetSnapshot == null)
                        return;
                    float nowD = Vector3.Distance(_local.transform.position, targetSnapshot.transform.position);
                    if (nowD > interactDistance)
                        return;
                    if (useFriendSidebarFlow)
                        StartCoroutine(CoHandleFriendInteract(targetSnapshot));
                    else
                        OpenWithTarget(targetSnapshot);
                }, priority: 140);
            }
        }

        IEnumerator CoRefreshFriendIdsLoop()
        {
            var wait = new WaitForSeconds(3f);
            while (enabled)
            {
                if (_api != null && _api.IsReady)
                {
                    yield return _api.CoFriendsList(
                        r =>
                        {
                            _friendIds.Clear();
                            if (r?.friends == null) return;
                            for (int i = 0; i < r.friends.Length; i++)
                            {
                                var f = r.friends[i];
                                if (f != null && f.user_id > 0)
                                {
                                    _friendIds.Add(f.user_id);
                                    _friendDisplayById[f.user_id] = (f.username ?? "").Trim();
                                }
                            }
                        },
                        _ => { });
                }
                yield return wait;
            }
            _friendRefresh = null;
        }

        IEnumerator CoHandleFriendInteract(CoopNetworkPlayer target)
        {
            _handlingInteract = true;
            if (target == null)
            {
                _handlingInteract = false;
                yield break;
            }
            bool isFriend = false;
            string listErr = null;
            yield return _api.CoFriendsList(
                r =>
                {
                    if (r?.friends == null) return;
                    for (int i = 0; i < r.friends.Length; i++)
                    {
                        var f = r.friends[i];
                        if (f != null && f.user_id == target.userId)
                        {
                            isFriend = true;
                            break;
                        }
                    }
                },
                e => listErr = e);
            if (target == null)
            {
                _handlingInteract = false;
                yield break;
            }

            if (!string.IsNullOrEmpty(listErr))
            {
                Debug.LogWarning("[近距互动] 好友列表查询失败: " + listErr);
                _handlingInteract = false;
                yield break;
            }

            if (!isFriend)
            {
                if (string.IsNullOrWhiteSpace(target.username))
                {
                    Debug.LogWarning("[近距互动] 目标用户名为空，无法自动加好友。");
                    _handlingInteract = false;
                    yield break;
                }

                float now = Time.unscaledTime;
                if (_lastAutoFriendRequestAt.TryGetValue(target.userId, out var lastAt) &&
                    now - lastAt < Mathf.Max(1f, autoFriendRequestCooldownSeconds))
                {
                    _handlingInteract = false;
                    yield break;
                }
                _lastAutoFriendRequestAt[target.userId] = now;

                string reqErr = null;
                yield return _api.CoFriendsRequest(
                    target.username,
                    () => Debug.Log($"[近距互动] 已自动发送好友申请 -> {target.username}({target.userId})"),
                    e => reqErr = e);
                if (target == null)
                {
                    _handlingInteract = false;
                    yield break;
                }
                if (!string.IsNullOrEmpty(reqErr))
                {
                    Debug.LogWarning("[近距互动] 自动加好友失败: " + reqErr);
                    // 失败时缩短冷却，允许稍后重试，但避免瞬间连发触发 429
                    _lastAutoFriendRequestAt[target.userId] = Time.unscaledTime - Mathf.Max(0.5f, autoFriendRequestCooldownSeconds - 2f);
                }
                else
                    _friendIds.Add(target.userId);
                _handlingInteract = false;
                yield break;
            }

            var sidebar = FindObjectOfType<MultiplayerFriendChatSidebarController>(true);
            if (sidebar != null)
                sidebar.OpenChatWithUserId(target.userId, ResolveTargetDisplayName(target));
            else
                Debug.LogWarning("[近距互动] 未找到 MultiplayerFriendChatSidebarController，无法打开好友聊天。");
            _handlingInteract = false;
        }

        void LateUpdate()
        {
            if (!useSharedPromptPanel) return;
            if (_sharedPromptNameTmp == null && _sharedPromptTypeTmp == null) return;
            InteractPromptSharedLabelFrame.ApplyBestToSharedTmp(_sharedPromptNameTmp, _sharedPromptTypeTmp);
        }

        void ResolveSharedPromptRefs()
        {
            _sharedPromptNameTmp = null;
            _sharedPromptTypeTmp = null;
            _sharedOfferGroupId = 0;
            if (!useSharedPromptPanel) return;
            if (_mode2Interactor == null)
                _mode2Interactor = FindObjectOfType<InteractWithMouseAndMovementLock>(true);
            if (_mode2Interactor != null)
            {
                _sharedPromptNameTmp = _mode2Interactor.interactNameText;
                _sharedPromptTypeTmp = _mode2Interactor.interactTypeText;
            }
            if (_sharedPromptNameTmp == null && _sharedPromptTypeTmp == null)
            {
                RectTransform panelIgnored = null;
                InteractPromptSharedUiLocator.TryAutoBind(ref panelIgnored, ref _sharedPromptNameTmp, ref _sharedPromptTypeTmp);
            }
            _sharedOfferGroupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(_sharedPromptNameTmp, _sharedPromptTypeTmp);
        }

        void OpenWithTarget(CoopNetworkPlayer target)
        {
            if (target == null)
                return;
            _target = target;
            _lastMsgId = 0;
            _log.text = "";
            _title.text = Loc.F("与 {0} 聊天", "Chat with {0}", BuildTargetPromptLabel(target));
            _panelRoot.SetActive(true);
            _input.text = "";
            _input.ActivateInputField();
            if (_poll != null)
                StopCoroutine(_poll);
            _poll = StartCoroutine(CoPoll());
        }

        void Close()
        {
            if (_poll != null)
                StopCoroutine(_poll);
            _poll = null;
            _panelRoot.SetActive(false);
        }

        IEnumerator CoPoll()
        {
            var wait = new WaitForSeconds(1f);
            while (_panelRoot.activeSelf && _target != null)
            {
                if (_target == null || _target.userId <= 0)
                    break;
                ProximityChatInboxResponse box = null;
                string sceneName = SceneManager.GetActiveScene().name;
                yield return _api.CoProximityChatInbox(sceneName, _lastMsgId, r => box = r, _ => { });
                if (box?.messages != null)
                {
                    for (int i = 0; i < box.messages.Length; i++)
                    {
                        var m = box.messages[i];
                        if (m.id > _lastMsgId)
                            _lastMsgId = m.id;
                        if (m.from_user_id == _target.userId || m.to_user_id == _target.userId)
                            _log.text += $"\n[{m.from_username}] {m.body}";
                    }
                }
                yield return wait;
            }
            _poll = null;
        }

        string ResolveTargetDisplayName(CoopNetworkPlayer target)
        {
            if (target == null)
                return "";
            string nick = (target.nickname ?? "").Trim();
            if (!string.IsNullOrEmpty(nick))
                return nick;
            if (_friendDisplayById.TryGetValue(target.userId, out var friendName) && !string.IsNullOrWhiteSpace(friendName))
                return friendName.Trim();
            string uname = (target.username ?? "").Trim();
            if (!string.IsNullOrEmpty(uname))
                return uname;
            return "";
        }

        string BuildTargetPromptLabel(CoopNetworkPlayer target)
        {
            if (target == null)
                return Loc.Choose("玩家", "Player");
            string display = ResolveTargetDisplayName(target);
            if (!string.IsNullOrEmpty(display))
                return $"{display}  (ID:{target.userId})";
            return Loc.F("玩家ID: {0}", "Player ID: {0}", target.userId);
        }

        public void UiSend()
        {
            if (_target == null || _target.userId <= 0) return;
            string txt = _input.text != null ? _input.text.Trim() : "";
            if (string.IsNullOrEmpty(txt)) return;
            StartCoroutine(_api.CoProximityChatSend(
                SceneManager.GetActiveScene().name,
                _target.userId,
                txt,
                () => _input.text = "",
                _ => { }));
        }

        CoopNetworkPlayer FindLocalCoop()
        {
            if (NetworkClient.localPlayer != null)
            {
                var onLocal = NetworkClient.localPlayer.GetComponent<CoopNetworkPlayer>();
                if (onLocal != null)
                    return onLocal;
            }
            var all = FindObjectsOfType<CoopNetworkPlayer>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].isLocalPlayer)
                    return all[i];
            }
            return null;
        }

        CoopNetworkPlayer FindClosestPeer(Vector3 fromPos)
        {
            var all = FindObjectsOfType<CoopNetworkPlayer>();
            CoopNetworkPlayer best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (p == null || p.isLocalPlayer) continue;
                float d = Vector3.Distance(fromPos, p.transform.position);
                if (d <= interactDistance && d < bestD)
                {
                    best = p;
                    bestD = d;
                }
            }
            return best;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("ProximityChatCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGo);

            _panelRoot = new GameObject("Panel");
            _panelRoot.transform.SetParent(canvasGo.transform, false);
            var rt = _panelRoot.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(480, 360);
            var bg = _panelRoot.AddComponent<Image>();
            bg.color = MultiplayerUiPalette.BgWhite;

            _title = CreateText(_panelRoot.transform, new Vector2(0, -24), new Vector2(460, 28), Loc.Choose("近距聊天", "Proximity Chat"), MultiplayerUiPalette.TextGold);
            _log = CreateText(_panelRoot.transform, new Vector2(0, -120), new Vector2(440, 170), "", MultiplayerUiPalette.TextBlack);
            _log.alignment = TextAlignmentOptions.TopLeft;
            _input = CreateInput(_panelRoot.transform, new Vector2(0, 110), new Vector2(440, 34), Loc.Choose("输入聊天内容", "Type a message"));
            CreateButton(_panelRoot.transform, new Vector2(0, 148), new Vector2(120, 32), Loc.Choose("发送", "Send"), UiSend);

            _panelRoot.SetActive(false);
        }

        static TMP_Text CreateText(Transform parent, Vector2 anchored, Vector2 size, string text, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.color = color;
            t.fontSize = 20;
            t.alignment = TextAlignmentOptions.Center;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            return t;
        }

        static TMP_InputField CreateInput(Transform parent, Vector2 anchored, Vector2 size, string placeholder)
        {
            var go = new GameObject("Input");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = MultiplayerUiPalette.InputBg;
            var input = go.AddComponent<TMP_InputField>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.color = MultiplayerUiPalette.TextBlack;
            txt.fontSize = 18;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8, 3);
            tr.offsetMax = new Vector2(-8, -3);
            input.textComponent = txt;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.color = new Color(MultiplayerUiPalette.TextMuted.r, MultiplayerUiPalette.TextMuted.g, MultiplayerUiPalette.TextMuted.b, 0.65f);
            ph.fontSize = 18;
            var phr = phGo.GetComponent<RectTransform>();
            phr.anchorMin = Vector2.zero;
            phr.anchorMax = Vector2.one;
            phr.offsetMin = new Vector2(8, 3);
            phr.offsetMax = new Vector2(-8, -3);
            input.placeholder = ph;
            return input;
        }

        static void CreateButton(Transform parent, Vector2 anchored, Vector2 size, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = MultiplayerUiPalette.BtnPrimaryBg;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            var tg = new GameObject("Text");
            tg.transform.SetParent(go.transform, false);
            var tt = tg.AddComponent<TextMeshProUGUI>();
            tt.text = text;
            tt.color = MultiplayerUiPalette.TextGold;
            tt.fontSize = 18;
            tt.alignment = TextAlignmentOptions.Center;
            var tr = tg.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }
    }
}
