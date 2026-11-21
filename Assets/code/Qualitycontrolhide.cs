using UnityEngine;

public class CurvePanelController : MonoBehaviour
{
    // ��קcurve��嵽�˴�
    public GameObject curvePanel;
    // ��ק��������嵽�˴���ȷ����curve�ڣ�
    public GameObject qualityPanel;

    // ��������á���ťʱ���ã���ʾ/����curve��壩
    public void ToggleCurvePanel()
    {
        curvePanel.SetActive(!curvePanel.activeSelf);
        // ���ر�curve��壬ͬʱ�رջ��������
        if (!curvePanel.activeSelf)
        {
            qualityPanel.SetActive(false);
            GameplayMenuCameraHelper.RestoreAfterMenuClosed();
        }
    }
}