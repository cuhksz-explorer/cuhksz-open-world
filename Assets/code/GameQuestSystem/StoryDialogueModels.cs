using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    [CreateAssetMenu(fileName = "Content1StoryDatabase", menuName = "Game Quest System/Story Dialogue Database")]
    public class StoryDialogueDatabase : ScriptableObject
    {
        public string sourceFolder = "Assets/content1";
        public string generatedAt;
        public List<StoryLocationDefinition> locations = new List<StoryLocationDefinition>();
        public List<StoryNpcDefinition> npcs = new List<StoryNpcDefinition>();
        public List<StoryDialogueDocument> documents = new List<StoryDialogueDocument>();

        public StoryDialogueDocument FindDocument(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId) || documents == null)
                return null;

            for (int i = 0; i < documents.Count; i++)
            {
                var doc = documents[i];
                if (doc != null && string.Equals(doc.documentId, documentId, StringComparison.OrdinalIgnoreCase))
                    return doc;
            }

            return null;
        }

        public StoryLocationDefinition FindLocation(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId) || locations == null)
                return null;

            for (int i = 0; i < locations.Count; i++)
            {
                var loc = locations[i];
                if (loc != null && string.Equals(loc.locationId, locationId, StringComparison.OrdinalIgnoreCase))
                    return loc;
            }

            return null;
        }

        public StoryNpcDefinition FindNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || npcs == null)
                return null;

            for (int i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                if (npc != null && string.Equals(npc.npcId, npcId, StringComparison.OrdinalIgnoreCase))
                    return npc;
            }

            return null;
        }
    }

    [Serializable]
    public class StoryLocationDefinition
    {
        public string locationId;
        public string displayName;
        public string sceneName;
        public Vector3 position;
        public bool needsConfirmation;
    }

    [Serializable]
    public class StoryNpcDefinition
    {
        public string npcId;
        public string displayName;
        [Tooltip("运行时实例化用；由编辑器从 prefabPath 同步。")]
        public GameObject prefab;
        public string prefabPath;
        public string defaultSceneName;
        public Vector3 defaultPosition;
        public bool needsPositionConfirmation;
    }

    [Serializable]
    public class StoryDialogueDocument
    {
        public string documentId;
        public string title;
        public string sourceAssetPath;
        public string chapterId;
        public QuestCategory category;
        public bool isRootTask;
        public bool isBranchDocument;
        public bool needsLocationConfirmation;
        public string completeEventId;
        public string npcId;
        public string locationId;
        public string targetSceneName;
        public Vector3 fallbackTargetPosition;
        public int rewardGold;
        public string finishLocationName;
        public List<StoryDialoguePage> pages = new List<StoryDialoguePage>();
        public List<StoryDialogueChoice> choices = new List<StoryDialogueChoice>();
    }

    [Serializable]
    public class StoryDialoguePage
    {
        public int pageNumber;
        public string speakerName;
        [TextArea(2, 8)] public string text;
    }

    [Serializable]
    public class StoryDialogueChoice
    {
        public string label;
        public string targetDocumentId;
    }
}
