using UnityEngine;

[CreateAssetMenu(fileName = "New Vehicle Data", menuName = "Racing/Vehicle Data")]
public class VehicleData : ScriptableObject
{
    [Header("基本信息")]
    public string vehicleName = "赛车名称";
    [TextArea(2, 5)] public string description = "车辆介绍文字";
    public Sprite thumbnail;          // 车辆图片（必须是 Sprite 类型）
    public GameObject vehiclePrefab;  // 车辆预制体（带控制器和物理组件）

    [Header("展示用数值（不影响物理）")]
    public int displayMaxSpeed = 200; // 最高时速 km/h
    [Range(1, 5)] public int accelerationRating = 3;
    [Range(1, 5)] public int handlingRating = 3;

    [Header("3D 预览调参（可选）")]
    public float previewScale = 1f;
    public float previewYawDegrees = 0f;
    public float previewPitchDegrees = 0f;
    public Vector3 previewOffset;
}