using UnityEngine;

namespace Experica.NetEnv 
{
    public class DotTrailLocal :NetEnvVisual 
    {
        
        public Vector3 Size = Vector3.one;
        public Color Color = Color.red;
        public Color TrailEndColor = Color.white;
        public float TrailWidthScale = 0.5f;
        public float TrailTime = 5f;
        public bool TrailEmit = true;
        protected TrailRenderer trailrenderer;
        public Vector3 TrailPosition = Vector3.zero;


        protected override void OnAwake()
        {
            base.OnAwake();
            trailrenderer = GetComponentInChildren<TrailRenderer>();
            
            OnColor(Color);
            OnSize(Size);
            OnTrailEndColor(TrailEndColor);
            OnTrailTime(TrailTime);
            OnTrailWidthScale(TrailWidthScale);
            OnTrailEmit(TrailEmit);
        }
        public Vector3 Position
        {
            get => TrailPosition;
            set
            {
                TrailPosition = value;
     
                if (transform != null)
                    transform.position = value;
            }
        }

        protected override void OnVisible(bool p, bool c)
        {
            renderer.enabled = c;
            trailrenderer.enabled = c;
        }

       
        public void OnSize(Vector3 c)
        {
            transform.localScale = c;
            trailrenderer.widthMultiplier = c.x * TrailWidthScale;
        }

        public void OnColor(Color c)
        {
            renderer.material.SetColor("_Color", c);
            trailrenderer.startColor = c;
        }

        public void OnTrailEndColor(Color c)
        {
            trailrenderer.endColor = c;
        }

        public void OnTrailTime(float c)
        {
            trailrenderer.time = c;
        }

        public void OnTrailWidthScale(float c)
        {
            trailrenderer.widthMultiplier = c * transform.localScale.x;
        }

        public void OnTrailEmit(bool c)
        {
            trailrenderer.emitting = c;
        }
    }
}