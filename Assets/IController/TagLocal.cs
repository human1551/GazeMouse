using UnityEngine;

namespace Experica.NetEnv
{
    public class TagLocal : MonoBehaviour 
    {
        
        [SerializeField]public float TagSize = 2f;
        [SerializeField]public float TagMargin = 2f;
        public AprilTag TagID = AprilTag.tag25_09_00000;
        public Corner TagCorner = Corner.BottomLeft;

        public INetEnvCamera netenvcamera; 
        new Renderer renderer;

        void Awake()
        {
            renderer = GetComponent<Renderer>();
            
            if (netenvcamera == null)
                netenvcamera = GetComponentInParent<INetEnvCamera>();
            UpdateTagPosition();
            OnTagSize(TagSize);
            OnTagID(TagID);
        }


        Vector3 gettagposition(Corner corner, float size, float margin = 0f)
        {
            var h = netenvcamera.Height;
            var w = netenvcamera.Width;
            var z = netenvcamera.NearPlane;
            return corner switch
            {
                Corner.TopLeft => new Vector3((-w + size) / 2f + margin, (h - size) / 2f - margin, z),
                Corner.TopRight => new Vector3((w - size) / 2f - margin, (h - size) / 2f - margin, z),
                Corner.BottomLeft => new Vector3((-w + size) / 2f + margin, (-h + size) / 2f + margin, z),
                Corner.BottomRight => new Vector3((w - size) / 2f - margin, (-h + size) / 2f + margin, z),
                _ => new Vector3(0f, 0f, z),
            };
        }

        
        public void UpdateTagPosition()
        {
            transform.localPosition = gettagposition(TagCorner, TagSize, TagMargin);
        }

        public void OnTagSize(float c)
        {
            transform.localScale = new Vector3(c, c, 1f);
            UpdateTagPosition();
        }

        public void OnTagMargin(float c) => UpdateTagPosition();

        public void OnTagCorner(Corner c) => UpdateTagPosition();

        public void OnTagID(AprilTag c)
        {
            if ($"Assets/NetEnv/Element/AprilTag/{c}.svg".QueryTexture(out Texture t))
            {
                renderer.material.SetTexture("_Image", t);
            }
        }

        public float TagSurfaceMargin => TagMargin + TagSize * (TagID.ToString().StartsWith("tag25") ? 1f / 9f : 1f / 10f);
    }
}