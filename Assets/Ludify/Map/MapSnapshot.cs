using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ludify.Map
{
    /// <summary>
    /// Renders the current scene from straight above into a texture, so the map always matches
    /// whatever the world looks like right now (no hand-made map image to keep in sync).
    /// North (+Z) is up; +X is right.
    /// </summary>
    public sealed class MapSnapshot : IDisposable
    {
        const int Resolution = 2048;
        static readonly Color GroundFallback = new Color(0.2f, 0.25f, 0.2f);

        /// <summary>World-space XZ area covered by the texture (square).</summary>
        public Rect WorldRect { get; }
        public RenderTexture Texture { get; }

        readonly float _topY;

        public MapSnapshot(Bounds worldBounds)
        {
            float size = Mathf.Max(worldBounds.size.x, worldBounds.size.z);
            Vector2 center = new Vector2(worldBounds.center.x, worldBounds.center.z);
            WorldRect = new Rect(center - Vector2.one * size / 2, Vector2.one * size);
            _topY = worldBounds.max.y;

            Texture = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32)
            {
                name = "MapSnapshot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,
            };
            Texture.Create();
        }

        /// <summary>Terrain area if there is one, otherwise everything with a renderer.</summary>
        public static Bounds FindWorldBounds()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Vector3 size = terrain.terrainData.size;
                Vector3 pos = terrain.transform.position;
                var bounds = new Bounds(pos + size / 2, size);
                // Include buildings taller than the terrain's height range.
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if (r.bounds.max.y > bounds.max.y && bounds.Contains(new Vector3(r.bounds.center.x, bounds.center.y, r.bounds.center.z)))
                        bounds.Encapsulate(new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z));
                return bounds;
            }

            var all = new Bounds(Vector3.zero, Vector3.one * 100);
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                all.Encapsulate(r.bounds);
            return all;
        }

        Camera _camera;
        bool _fog;

        /// <summary>
        /// Re-renders the scene into <see cref="Texture"/> during the next frame. The map camera is
        /// enabled for exactly one frame, which works reliably in URP from inside the game loop.
        /// </summary>
        public IEnumerator Capture()
        {
            if (_camera == null) CreateCamera();
            _camera.enabled = true;
            yield return null;
            if (_camera != null) _camera.enabled = false;
        }

        void CreateCamera()
        {
            var go = new GameObject("MapSnapshotCamera");
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = WorldRect.width / 2;
            _camera.aspect = 1;
            float height = _topY + 50f;
            go.transform.SetPositionAndRotation(new Vector3(WorldRect.center.x, height, WorldRect.center.y),
                                                Quaternion.Euler(90, 0, 0));
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = height + 500f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = RenderSettings.fog ? RenderSettings.fogColor : GroundFallback;
            _camera.depth = -100; // render before the main camera
            _camera.targetTexture = Texture;

            // Fog would wash out a camera this high up. Turn it off for this camera only.
            RenderPipelineManager.beginCameraRendering += BeforeRender;
            RenderPipelineManager.endCameraRendering += AfterRender;
        }

        void BeforeRender(ScriptableRenderContext _, Camera c)
        {
            if (c != _camera) return;
            _fog = RenderSettings.fog;
            RenderSettings.fog = false;
        }

        void AfterRender(ScriptableRenderContext _, Camera c)
        {
            if (c == _camera) RenderSettings.fog = _fog;
        }

        /// <summary>World position → normalized map coordinates (0..1, origin bottom-left).</summary>
        public Vector2 WorldToUv(Vector3 world) =>
            new Vector2((world.x - WorldRect.xMin) / WorldRect.width, (world.z - WorldRect.yMin) / WorldRect.height);

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeRender;
            RenderPipelineManager.endCameraRendering -= AfterRender;
            if (_camera != null) UnityEngine.Object.Destroy(_camera.gameObject);
            if (Texture != null)
            {
                Texture.Release();
                UnityEngine.Object.Destroy(Texture);
            }
        }
    }
}
