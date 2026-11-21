using UnityEngine;
using UnityEngine.UI;

public class TeleportController : MonoBehaviour
{
    // ��ҵ�Transform�������Ҫ��Inspector�и�ֵ��
    [Tooltip("��ק��Ҷ�������")]
    public Transform playerTransform;

    // ����Ŀ��λ�ã������ǳ����еĿ����壬��Inspector�и�ֵ��
    [Tooltip("��ק��Ϊ����Ŀ��Ŀ����嵽����")]
    public Transform targetPosition;

    // ���ͷ������󶨵���ť����¼���
    public void TeleportToTarget()
    {
        // ����Ҫ����Ƿ��Ѹ�ֵ
        if (playerTransform == null)
        {
            Debug.LogError("���ȸ�ֵ���Transform��");
            return;
        }

        if (targetPosition == null)
        {
            Debug.LogError("�������ô���Ŀ��λ�ã�");
            return;
        }

        var movement = playerTransform.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.Teleport(targetPosition.position);
        else
            playerTransform.position = targetPosition.position;

        Debug.Log($"�Ѵ��͵�Ŀ��λ�ã�{targetPosition.position}");
    }
}