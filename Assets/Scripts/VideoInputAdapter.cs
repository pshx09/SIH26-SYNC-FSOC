using UnityEngine;
using UnityEngine.Video;
using FSOC.Contracts;

namespace FSC.Core
{
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoInputAdapter : MonoBehaviour
    {
        public static VideoInputAdapter Instance { get; private set; }
        
        public VideoPlayer videoPlayer;
        public RenderTexture targetTexture;
        public BeaconDetector beaconDetector;
        
        public bool isVideoModeActive = false;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            videoPlayer = GetComponent<VideoPlayer>();
        }

        public static VideoInputAdapter GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("VideoInputAdapter");
                Instance = go.AddComponent<VideoInputAdapter>();
                
                Instance.targetTexture = new RenderTexture(640, 480, 0, RenderTextureFormat.ARGB32);
                Instance.targetTexture.Create();
                
                Instance.videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                Instance.videoPlayer.targetTexture = Instance.targetTexture;
                Instance.beaconDetector = Object.FindFirstObjectByType<BeaconDetector>();
                
                DontDestroyOnLoad(go);
            }
            return Instance;
        }
        
        public void LoadVideo(string path)
        {
            videoPlayer.url = path;
            videoPlayer.Prepare();
        }
        
        public void Play() => videoPlayer.Play();
        public void Pause() => videoPlayer.Pause();
        public void Stop() => videoPlayer.Stop();
        
        private void Update()
        {
            if (isVideoModeActive && beaconDetector != null && targetTexture != null)
            {
                beaconDetector.sensorTexture = targetTexture;
            }
        }
    }
}
