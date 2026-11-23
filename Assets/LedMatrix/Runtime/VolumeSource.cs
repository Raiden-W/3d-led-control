using UnityEngine;

namespace LedMatrix
{
    // New content inputs write into the same matrix-local volume in version 2.
    public abstract class VolumeSource : MonoBehaviour
    {
        public abstract void WriteVolume(MatrixVolumeRenderer target);
    }
}
