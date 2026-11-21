using UnityEngine;

namespace DormMall
{
    /// <summary>
    /// 挂在「进入寝室」传送门旁：离开寝室时回到此 Transform 的位置（而非点击瞬间的主角坐标）。
    /// </summary>
    public class DormReturnAnchor : MonoBehaviour
    {
        [Tooltip("为真时使用本物体 Transform 作为返回点；否则仍用主角当前位置。")]
        public bool useThisTransform = true;

        [Tooltip("在返回点位置上再偏移（米）。")]
        public Vector3 positionOffset;

        [Tooltip("为真时使用本物体旋转；否则使用主角当前朝向。")]
        public bool useThisRotation = true;

        public bool TryGetReturnPoint(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!useThisTransform)
                return false;

            position = transform.position + transform.TransformVector(positionOffset);
            rotation = useThisRotation ? transform.rotation : Quaternion.identity;
            return true;
        }
    }
}
