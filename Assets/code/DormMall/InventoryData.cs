using System;
using System.Collections.Generic;
using UnityEngine;

namespace DormMall
{
    /// <summary>�ѹ�����Ʒ���ݣ������ã�</summary>
    [Serializable]
    public class PurchasedItemData
    {
        public int id;
        public string productName;
        public string category;
        public string iconPath;
    }

    /// <summary>�����¼�洢���̳Ǻͱ������ã�������أ�</summary>
    public static class InventoryData
    {
        private static List<PurchasedItemData> _items = new List<PurchasedItemData>();

        [Serializable]
        class PurchasedItemArrayWrapper
        {
            public PurchasedItemData[] items;
        }

        /// <summary>�����ѹ���Ʒ</summary>
        public static void AddItem(int id, string productName, string category, string iconPath)
        {
            _items.Add(new PurchasedItemData
            {
                id = id,
                productName = productName ?? "",
                category = category ?? "",
                iconPath = iconPath ?? ""
            });
            Debug.Log($"[InventoryData] �Ѽ�¼����: {productName}����ǰ�� {_items.Count} ��");
        }

        /// <summary>�����ѹ���Ʒ���� ProductEntry��</summary>
        public static void AddItem(ProductEntry entry)
        {
            if (entry == null) return;
            AddItem(entry.id, entry.productName, entry.category, entry.iconPath ?? "");
        }

        /// <summary>��ȡ�����ѹ���Ʒ</summary>
        public static IReadOnlyList<PurchasedItemData> GetItems() => _items;

        /// <summary>�Ƴ�ָ���������ѹ���Ʒ�����õ���������ã�</summary>
        public static void RemoveItemAt(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _items.RemoveAt(index);
            Debug.Log($"[InventoryData] ���Ƴ�������Ʒ��ʣ�� {_items.Count} ��");
        }

        /// <summary>��չ����¼��������Ϸ��</summary>
        public static void Clear() => _items.Clear();

        /// <summary>���л��������ݣ�������������ͻ��㲥��</summary>
        public static string SerializeItemsJson()
        {
            var arr = _items != null ? _items.ToArray() : Array.Empty<PurchasedItemData>();
            return JsonUtility.ToJson(new PurchasedItemArrayWrapper { items = arr });
        }

        /// <summary>�ͻ�Ӧ�÷����������ա�</summary>
        public static void ApplySerializedItems(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;
            PurchasedItemArrayWrapper wrapper = null;
            try
            {
                wrapper = JsonUtility.FromJson<PurchasedItemArrayWrapper>(json);
            }
            catch
            {
                return;
            }
            _items.Clear();
            if (wrapper?.items == null)
                return;
            for (int i = 0; i < wrapper.items.Length; i++)
            {
                var x = wrapper.items[i];
                if (x == null) continue;
                _items.Add(new PurchasedItemData
                {
                    id = x.id,
                    productName = x.productName ?? "",
                    category = x.category ?? "",
                    iconPath = x.iconPath ?? ""
                });
            }
        }
    }
}
