﻿//------------------------------------------------------------------------------------------------------------------
// Global Snow
// Created by Ramiro Oliva (Kronnect)
//------------------------------------------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace GlobalSnowEffect {

    public enum MASK_TEXTURE_BRUSH_MODE {
        AddSnow = 0,
        RemoveSnow = 1
    }

    public enum GROUND_CHECK {
        None = 0,
        CharacterController = 1,
        RayCast = 2
    }

    public enum DECAL_TEXTURE_RESOLUTION {
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
        _8192 = 8192
    }


    public enum BORDERS_EFFECT {
        None,
        Smooth
    }

    public enum SNOW_QUALITY {
        FlatShading = 10,
        NormalMapping = 20,
        ReliefMapping = 30
    }

    public enum SNOW_COVERAGE_UPDATE_METHOD {
        EveryFrame,
        Discrete,
        Manual,
        Disabled
    }

    public enum DEFERRED_CAMERA_EVENT {
        BeforeReflections = 0,
        BeforeLighting = 1
    }

    static class CameraEventExtensions {
        public static CameraEvent ToUnityCameraEvent(this DEFERRED_CAMERA_EVENT e) {
            switch (e) {
                case DEFERRED_CAMERA_EVENT.BeforeLighting:
                    return CameraEvent.BeforeLighting;
                default:
                    return CameraEvent.BeforeReflections;
            }
        }
    }

    struct SnowColliderInfo {
        public Vector3 position;
        public Vector3 forward;
        public float markSize;
    }



    public delegate void OnUpdatePropertiesEvent();

    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [HelpURL("http://kronnect.com/taptapgo")]
    [ImageEffectAllowedInSceneView]
    public class GlobalSnow : MonoBehaviour {
        const string ZENITH_CAM = "GlobalSnowZenithCam";
        const string REP_CAM = "GlobalSnowRepCam";
        const string SNOW_PARTICLE_SYSTEM = "SnowParticleSystem";
        const int FOOTPRINT_TEXTURE_RESOLUTION = 2048;
        const int SNOW_PARTICLES_LAYER = 2;

        // keywords for snow renderer
        const string SKW_FLAT_SHADING = "GLOBALSNOW_FLAT_SHADING";
        const string SKW_RELIEF = "GLOBALSNOW_RELIEF";
        const string SKW_OCLUSSION = "GLOBALSNOW_OCCLUSION";
        const string SKW_FOOTPRINTS = "GLOBALSNOW_FOOTPRINTS";
        const string SKW_TERRAIN_MARKS = "GLOBALSNOW_TERRAINMARKS";
        const string SKW_GLOBALSNOW_OPAQUE_CUTOUT = "GLOBALSNOW_OPAQUE_CUTOUT";
        const string SKW_REMOVE_LEAVES = "GLOBALSNOW_DISCARD_LEAVES";
        // for compose shader
        const string SKW_FORCE_STEREO_RENDERING = "FORCE_STEREO_RENDERING";
        const string SKW_USE_DISTANT_SNOW = "DISTANT_SNOW";
        const string SKW_JUST_DISTANT_SNOW = "JUST_DISTANT_SNOW";
        const string SKW_NO_FROST = "NO_FROST";
        const string SKW_NO_SNOW = "NO_SNOW";
        // for distance shader
        const string SKW_GBUFFER_NORMALS = "NORMALS_GBUFFER";
        const string SKW_IGNORE_NORMALS = "IGNORE_NORMALS";
        const string SKW_IGNORE_COVERAGE = "IGNORE_COVERAGE";

        #region Public properties

        public OnUpdatePropertiesEvent OnUpdateProperties;

        static GlobalSnow _snow;

        public static GlobalSnow instance {
            get {
                if (_snow == null) {
                    if (Camera.main != null)
                        _snow = Camera.main.GetComponent<GlobalSnow>();
                    if (_snow == null) {
                        foreach (Camera camera in Camera.allCameras) {
                            _snow = camera.GetComponent<GlobalSnow>();
                            if (_snow != null)
                                break;
                        }
                    }
                }
                return _snow;
            }
        }

        [SerializeField]
        GameObject
                        _sun;

        public GameObject sun {
            get { return _sun; }
            set {
                if (value != _sun) {
                    _sun = value;
                }
            }
        }


        [SerializeField]
        bool _enableWMAPI = false;

        /// <summary>
        /// Enables World Manager API integration
        /// </summary>
        /// <value><c>true</c> if enable WMAP; otherwise, <c>false</c>.</value>
        public bool enableWMAPI {
            get { return _enableWMAPI; }
            set {
                if (value != _debugSnow) {
                    _enableWMAPI = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _forceForwardRenderingPath = false;

        public bool forceForwardRenderingPath {
            get { return _forceForwardRenderingPath; }
            set {
                if (value != _debugSnow) {
                    _forceForwardRenderingPath = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _debugSnow = false;

        public bool debugSnow {
            get { return _debugSnow; }
            set {
                if (value != _debugSnow) {
                    _debugSnow = value;
                }
            }
        }

        [SerializeField]
        DEFERRED_CAMERA_EVENT _deferredCameraEvent = DEFERRED_CAMERA_EVENT.BeforeReflections;

        public DEFERRED_CAMERA_EVENT deferredCameraEvent {
            get { return _deferredCameraEvent; }
            set {
                if (value != _deferredCameraEvent) {
                    _deferredCameraEvent = value;
                    UpdateProperties();
                }
            }
        }



        [SerializeField]
        bool _showSnowInSceneView;

        public bool showSnowInSceneView {
            get { return _showSnowInSceneView; }
            set {
                if (value != _showSnowInSceneView) {
                    _showSnowInSceneView = value;
                }
            }
        }

        [SerializeField]
        bool _forceSPSR = false;

        public bool forceSPSR {
            get { return _forceSPSR; }
            set {
                if (value != _forceSPSR) {
                    _forceSPSR = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        float _minimumAltitude = -10f;

        public float minimumAltitude {
            get { return _minimumAltitude; }
            set {
                if (value != _minimumAltitude) {
                    _minimumAltitude = value;
                    UpdateSnowData2(_minimumAltitude);
                }
            }
        }


        [SerializeField]
        float _minimumAltitudeVegetationOffset = 0f;

        public float minimumAltitudeVegetationOffset {
            get { return _minimumAltitudeVegetationOffset; }
            set {
                if (value != _minimumAltitudeVegetationOffset) {
                    _minimumAltitudeVegetationOffset = value;
                    UpdateSnowData2(_minimumAltitude);
                }
            }
        }

        [SerializeField]
        Color _snowTint = Color.white;

        public Color snowTint {
            get { return _snowTint; }
            set {
                if (value != _snowTint) {
                    _snowTint = value;
                    UpdateSnowTintColor();
                }
            }
        }



        [SerializeField]
        [Range(0, 250f)]
        float _altitudeScatter = 20f;

        public float altitudeScatter {
            get { return _altitudeScatter; }
            set {
                if (value != _altitudeScatter) {
                    _altitudeScatter = value;
                    UpdateSnowData2(_minimumAltitude);
                }
            }
        }


        [SerializeField]
        [Range(0, 500f)]
        float _altitudeBlending = 25f;

        public float altitudeBlending {
            get { return _altitudeBlending; }
            set {
                if (value != _altitudeBlending) {
                    _altitudeBlending = value;
                    UpdateSnowData6();
                }
            }
        }



        [SerializeField]
        bool _distanceOptimization = false;

        public bool distanceOptimization {
            get { return _distanceOptimization; }
            set {
                if (value != _distanceOptimization) {
                    _distanceOptimization = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        float _detailDistance = 100f;

        public float detailDistance {
            get { return _detailDistance; }
            set {
                if (value != _detailDistance) {
                    _detailDistance = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        Color _distanceSnowColor = new Color(0.7882f, 0.8235f, 0.9921f);

        public Color distanceSnowColor {
            get { return _distanceSnowColor; }
            set {
                if (value != _distanceSnowColor) {
                    _distanceSnowColor = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _distanceIgnoreNormals = false;

        public bool distanceIgnoreNormals {
            get { return _distanceIgnoreNormals; }
            set {
                if (value != _distanceIgnoreNormals) {
                    _distanceIgnoreNormals = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        bool _distanceIgnoreCoverage = false;

        public bool distanceIgnoreCoverage {
            get { return _distanceIgnoreCoverage; }
            set {
                if (value != _distanceIgnoreCoverage) {
                    _distanceIgnoreCoverage = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _showCoverageGizmo = false;

        public bool showCoverageGizmo {
            get { return _showCoverageGizmo; }
            set {
                if (value != _showCoverageGizmo) {
                    _showCoverageGizmo = value;
                }
            }
        }


        [SerializeField]
        bool _smoothCoverage = true;

        public bool smoothCoverage {
            get { return _smoothCoverage; }
            set {
                if (value != _smoothCoverage) {
                    _smoothCoverage = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(1, 4)]
        int _coverageExtension = 1;

        public int coverageExtension {
            get { return _coverageExtension; }
            set {
                if (value != _coverageExtension) {
                    _coverageExtension = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(1, 4)]
        int _coverageResolution = 1;

        public int coverageResolution {
            get { return _coverageResolution; }
            set {
                if (value != _coverageResolution) {
                    _coverageResolution = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(0, 0.5f)]
        float _groundCoverage = 0f;

        public float groundCoverage {
            get { return _groundCoverage; }
            set {
                if (value != _groundCoverage) {
                    _groundCoverage = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(0, 1f)]
        float _slopeThreshold = 0.7f;

        public float slopeThreshold {
            get { return _slopeThreshold; }
            set {
                if (value != _slopeThreshold) {
                    _slopeThreshold = value;
                    UpdateSnowData5();
                }
            }
        }

        [SerializeField]
        [Range(0, 1f)]
        float _slopeSharpness = 0.5f;

        public float slopeSharpness {
            get { return _slopeSharpness; }
            set {
                if (value != _slopeSharpness) {
                    _slopeSharpness = value;
                    UpdateSnowData5();
                }
            }
        }

        [SerializeField]
        [Range(0, 1f)]
        float _slopeNoise = 0.5f;

        public float slopeNoise {
            get { return _slopeNoise; }
            set {
                if (value != _slopeNoise) {
                    _slopeNoise = value;
                    UpdateSnowData5();
                }
            }
        }

        [SerializeField]
        [Range(0, 1f)]
        float _distanceSlopeThreshold = 0.7f;

        public float distanceSlopeThreshold {
            get { return _distanceSlopeThreshold; }
            set {
                if (value != _distanceSlopeThreshold) {
                    _distanceSlopeThreshold = value;
                    UpdateSnowData5();
                }
            }
        }



        [SerializeField]
        bool _coverageMask = false;

        public bool coverageMask {
            get { return _coverageMask; }
            set {
                if (value != _coverageMask) {
                    _coverageMask = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        Texture2D _coverageMaskTexture = null;

        public Texture2D coverageMaskTexture {
            get { return _coverageMaskTexture; }
            set {
                if (value != _coverageMaskTexture) {
                    _coverageMaskTexture = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        Vector3 _coverageMaskWorldSize = new Vector3(2000, 0, 2000);

        public Vector3 coverageMaskWorldSize {
            get { return _coverageMaskWorldSize; }
            set {
                if (value != _coverageMaskWorldSize) {
                    _coverageMaskWorldSize = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        Vector3 _coverageMaskWorldCenter = new Vector3(0, 0, 0);

        public Vector3 coverageMaskWorldCenter {
            get { return _coverageMaskWorldCenter; }
            set {
                if (value != _coverageMaskWorldCenter) {
                    _coverageMaskWorldCenter = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        SNOW_QUALITY _snowQuality = SNOW_QUALITY.ReliefMapping;

        public SNOW_QUALITY snowQuality {
            get { return _snowQuality; }
            set {
                if (value != _snowQuality) {
                    _snowQuality = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        SNOW_COVERAGE_UPDATE_METHOD _coverageUpdateMethod = SNOW_COVERAGE_UPDATE_METHOD.Discrete;

        public SNOW_COVERAGE_UPDATE_METHOD coverageUpdateMethod {
            get { return _coverageUpdateMethod; }
            set {
                if (value != _coverageUpdateMethod) {
                    _coverageUpdateMethod = value;
                }
            }
        }

        public bool coverageDepthDebug;


        [SerializeField]
        [Range(0.05f, 0.3f)]
        float _reliefAmount = 0.3f;

        public float reliefAmount {
            get { return _reliefAmount; }
            set {
                if (value != _reliefAmount) {
                    reliefAmount = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        bool _occlusion = true;

        public bool occlusion {
            get { return _occlusion; }
            set {
                if (value != _occlusion) {
                    _occlusion = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(0.01f, 5f)]
        float _occlusionIntensity = 1.2f;

        public float occlusionIntensity {
            get { return _occlusionIntensity; }
            set {
                if (value != _occlusionIntensity) {
                    _occlusionIntensity = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        float _glitterStrength = 0.75f;

        public float glitterStrength {
            get { return _glitterStrength; }
            set {
                if (value != _glitterStrength) {
                    _glitterStrength = Mathf.Max(0, value);
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _footprints = false;

        public bool footprints {
            get { return _footprints; }
            set {
                if (value != _footprints) {
                    _footprints = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        Texture2D _footprintsTexture;

        public Texture2D footprintsTexture {
            get { return _footprintsTexture; }
            set {
                if (value != _footprintsTexture) {
                    _footprintsTexture = value;
                }
            }
        }


        [SerializeField]
        bool _footprintsAutoFPS = true;

        public bool footprintsAutoFPS {
            get { return _footprintsAutoFPS; }
            set {
                if (value != _footprintsAutoFPS) {
                    _footprintsAutoFPS = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(1f, 240f)]
        int _footprintsDuration = 60;

        public int footprintsDuration {
            get { return _footprintsDuration; }
            set {
                if (value != _footprintsDuration) {
                    _footprintsDuration = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(0.04f, 1f)]
        float _footprintsScale = 1f;

        public float footprintsScale {
            get { return _footprintsScale; }
            set {
                if (value != _footprintsScale) {
                    _footprintsScale = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(0.05f, 0.5f)]
        float _footprintsObscurance = 0.1f;

        public float footprintsObscurance {
            get { return _footprintsObscurance; }
            set {
                if (value != _footprintsObscurance) {
                    _footprintsObscurance = value;
                    UpdateProperties();
                }
            }
        }



        [SerializeField]
        bool _snowfall = true;

        public bool snowfall {
            get { return _snowfall; }
            set {
                if (value != _snowfall) {
                    _snowfall = value;
                    UpdateSnowfallProperties();
                }
            }
        }


        [SerializeField]
        [Range(0.001f, 1f)]
        float _snowfallIntensity = 0.1f;

        public float snowfallIntensity {
            get { return _snowfallIntensity; }
            set {
                if (value != _snowfallIntensity) {
                    _snowfallIntensity = value;
                    UpdateSnowfallProperties();
                }
            }
        }


        [SerializeField]
        float _snowfallSpeed = 1f;

        public float snowfallSpeed {
            get { return _snowfallSpeed; }
            set {
                if (value != _snowfallSpeed) {
                    _snowfallSpeed = value;
                    UpdateSnowfallProperties();
                }
            }
        }


        [SerializeField]
        [Range(10, 200)]
        float _snowfallDistance = 100f;

        public float snowfallDistance {
            get { return _snowfallDistance; }
            set {
                if (value != _snowfallDistance) {
                    _snowfallDistance = value;
                    UpdateSnowfallProperties();
                }
            }
        }


        [SerializeField]
        bool _snowfallUseIllumination = false;

        public bool snowfallUseIllumination {
            get { return _snowfallUseIllumination; }
            set {
                if (value != _snowfallUseIllumination) {
                    _snowfallUseIllumination = value;
                    UpdateSnowfallProperties();
                }
            }
        }


        [SerializeField]
        bool _snowfallReceiveShadows = false;

        public bool snowfallReceiveShadows {
            get { return _snowfallReceiveShadows; }
            set {
                if (value != _snowfallReceiveShadows) {
                    _snowfallReceiveShadows = value;
                    UpdateSnowfallProperties();
                }
            }
        }


        [SerializeField]
        [Range(0f, 2f)]
        float _maxExposure = 0.85f;

        public float maxExposure {
            get { return _maxExposure; }
            set {
                if (value != _maxExposure) {
                    _maxExposure = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(0f, 1f)]
        float _smoothness = 0.9f;

        public float smoothness {
            get { return _smoothness; }
            set {
                if (value != _smoothness) {
                    _smoothness = value;
                    UpdateSnowData6();
                }
            }
        }

        [SerializeField]
        [Range(0f, 2f)]
        float _snowAmount = 1f;

        public float snowAmount {
            get { return _snowAmount; }
            set {
                if (value != _snowAmount) {
                    _snowAmount = Mathf.Clamp(value, 0, 2f);
                    UpdateSnowData3();
                    UpdateSnowData4();
                    UpdateSnowData6();
                    UpdateSnowfallProperties();
                }
            }
        }


        [SerializeField]
        bool _cameraFrost = true;

        public bool cameraFrost {
            get { return _cameraFrost; }
            set {
                if (value != _cameraFrost) {
                    _cameraFrost = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(0.001f, 1.5f)]
        float _cameraFrostIntensity = 0.35f;

        public float cameraFrostIntensity {
            get { return _cameraFrostIntensity; }
            set {
                if (value != _cameraFrostIntensity) {
                    _cameraFrostIntensity = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(1f, 5f)]
        float _cameraFrostSpread = 1.2f;

        public float cameraFrostSpread {
            get { return _cameraFrostSpread; }
            set {
                if (value != _cameraFrostSpread) {
                    _cameraFrostSpread = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(0f, 1f)]
        float _cameraFrostDistortion = 0.25f;

        public float cameraFrostDistortion {
            get { return _cameraFrostDistortion; }
            set {
                if (value != _cameraFrostDistortion) {
                    _cameraFrostDistortion = value;
                    UpdateProperties();
                }
            }
        }

        public Camera snowCamera { get { return cameraEffect; } }


        public bool deferred {
            get {
                if (!_forceForwardRenderingPath && cameraEffect != null) {
                    return cameraEffect.actualRenderingPath == RenderingPath.DeferredShading;
                } else {
                    return false;
                }
            }
        }


        [SerializeField]
        bool _floatingPointNormalsBuffer;

        public bool floatingPointNormalsBuffer {
            get {
                return _floatingPointNormalsBuffer;
            }
            set {
                if (value != _floatingPointNormalsBuffer) {
                    _floatingPointNormalsBuffer = value;
                    needRebuildCommandBuffer = true;
                }

            }
        }

        [SerializeField]
        LayerMask
                        _layerMask = -1;

        public LayerMask layerMask {
            get { return _layerMask; }
            set {
                if (_layerMask != value) {
                    _layerMask = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        bool
                        _excludedCastShadows = true;

        public bool excludedCastShadows {
            get { return _excludedCastShadows; }
            set {
                if (_excludedCastShadows != value) {
                    _excludedCastShadows = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        LayerMask
                        _zenithalMask = -1;

        public LayerMask zenithalMask {
            get { return _zenithalMask; }
            set {
                if (_zenithalMask != value) {
                    _zenithalMask = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        int
                        _defaultExclusionLayer = 27;

        public int defaultExclusionLayer {
            get { return _defaultExclusionLayer; }
            set {
                if (_defaultExclusionLayer != value) {
                    _defaultExclusionLayer = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _terrainMarks = false;

        public bool terrainMarks {
            get { return _terrainMarks; }
            set {
                if (value != _terrainMarks) {
                    _terrainMarks = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(1f, 240)]
        int _terrainMarksDuration = 180;

        public int terrainMarksDuration {
            get { return _terrainMarksDuration; }
            set {
                if (value != _terrainMarksDuration) {
                    _terrainMarksDuration = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(0f, 1f)]
        float _terrainMarksDefaultSize = 0.25f;

        public float terrainMarksDefaultSize {
            get { return _terrainMarksDefaultSize; }
            set {
                if (value != _terrainMarksDefaultSize) {
                    _terrainMarksDefaultSize = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        float _terrainMarksRoofMinDistance = 0.5f;

        public float terrainMarksRoofMinDistance {
            get { return _terrainMarksRoofMinDistance; }
            set {
                if (value != _terrainMarksRoofMinDistance) {
                    _terrainMarksRoofMinDistance = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        bool _terrainMarksAutoFPS = false;

        public bool terrainMarksAutoFPS {
            get { return _terrainMarksAutoFPS; }
            set {
                if (value != _terrainMarksAutoFPS) {
                    _terrainMarksAutoFPS = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        [Range(0f, 1024f)]
        float _terrainMarksViewDistance = 200f;

        public float terrainMarksViewDistance {
            get { return _terrainMarksViewDistance; }
            set {
                if (value != _terrainMarksViewDistance) {
                    _terrainMarksViewDistance = value;
                    UpdateProperties();
                }
            }
        }



        [SerializeField]
        float _terrainMarksStepMaxDistance = 20f;

        public float terrainMarksStepMaxDistance {
            get { return _terrainMarksStepMaxDistance; }
            set {
                if (value != _terrainMarksStepMaxDistance) {
                    _terrainMarksStepMaxDistance = value;
                    UpdateProperties();
                }
            }
        }



        [SerializeField]
        float _terrainMarksRotationThreshold = 3f;

        public float terrainMarksRotationThreshold {
            get { return _terrainMarksRotationThreshold; }
            set {
                if (value != _terrainMarksRotationThreshold) {
                    _terrainMarksRotationThreshold = value;
                    UpdateProperties();
                }
            }
        }


        public DECAL_TEXTURE_RESOLUTION terrainMarksTextureSize = DECAL_TEXTURE_RESOLUTION._2048;

        [SerializeField]
        bool _updateSpeedTree = false;

        public bool updateSpeedTree {
            get { return _updateSpeedTree; }
            set {
                if (value != _updateSpeedTree) {
                    _updateSpeedTree = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _speedTreeRemoveLeaves = false;

        public bool speedTreeRemoveLeaves {
            get { return _speedTreeRemoveLeaves; }
            set {
                if (value != _speedTreeRemoveLeaves) {
                    _speedTreeRemoveLeaves = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _fixMaterials = false;

        public bool fixMaterials {
            get { return _fixMaterials; }
            set {
                if (value != _fixMaterials) {
                    _fixMaterials = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        bool _opaqueCutout = false;

        public bool opaqueCutout {
            get { return _opaqueCutout; }
            set {
                if (value != _opaqueCutout) {
                    _opaqueCutout = value;
                    UpdateProperties();
                }
            }
        }

        // Custom trees and grass shaders support for deferred
        [SerializeField]
        bool _fixTreeOpaque = false;

        public bool fixTreeOpaque {
            get { return _fixTreeOpaque; }
            set {
                if (value != _fixTreeOpaque) {
                    _fixTreeOpaque = value;
                    UpdateProperties();
                }
            }
        }



        [SerializeField]
        [Range(0f, 2f)]
        float _billboardCoverage = 1.4f;

        public float billboardCoverage {
            get { return _billboardCoverage; }
            set {
                if (value != _billboardCoverage) {
                    _billboardCoverage = value;
                    UpdateProperties();
                }
            }
        }

        [SerializeField]
        [Range(0f, 1f)]
        float _grassCoverage = 0.75f;

        public float grassCoverage {
            get { return _grassCoverage; }
            set {
                if (value != _grassCoverage) {
                    _grassCoverage = value;
                    UpdateProperties();
                }
            }
        }


        [SerializeField]
        GROUND_CHECK _groundCheck = GROUND_CHECK.None;

        public GROUND_CHECK groundCheck {
            get { return _groundCheck; }
            set {
                if (value != _groundCheck) {
                    _groundCheck = value;
                    needFootprintBlit = true;
                }
            }
        }


        [SerializeField]
        CharacterController _characterController;

        public CharacterController characterController {
            get { return _characterController; }
            set {
                if (value != _characterController) {
                    _characterController = value;
                    needFootprintBlit = true;
                }
            }
        }


        [SerializeField]
        float _groundDistance = 1f;

        public float groundDistance {
            get { return _groundDistance; }
            set {
                if (value != _groundDistance) {
                    _grassCoverage = value;
                    UpdateProperties();
                }
            }
        }



        [SerializeField]
        bool _maskEditorEnabled;

        public bool maskEditorEnabled {
            get { return _maskEditorEnabled; }
            set {
                if (value != _maskEditorEnabled) {
                    _maskEditorEnabled = value;
                }
            }
        }


        [SerializeField]
        int _maskTextureResolution = 1024;

        public int maskTextureResolution {
            get { return _maskTextureResolution; }
            set {
                if (value != _maskTextureResolution) {
                    _maskTextureResolution = value;
                }
            }
        }

        [SerializeField]
        MASK_TEXTURE_BRUSH_MODE _maskBrushMode = MASK_TEXTURE_BRUSH_MODE.RemoveSnow;

        public MASK_TEXTURE_BRUSH_MODE maskBrushMode {
            get { return _maskBrushMode; }
            set {
                if (value != _maskBrushMode) {
                    _maskBrushMode = value;
                }
            }
        }


        [SerializeField, Range(1, 128)]
        int _maskBrushWidth = 20;

        public int maskBrushWidth {
            get { return _maskBrushWidth; }
            set {
                if (value != _maskBrushWidth) {
                    _maskBrushWidth = value;
                }
            }
        }

        [SerializeField, Range(0, 1)]
        float _maskBrushFuzziness = 0.5f;

        public float maskBrushFuzziness {
            get { return _maskBrushFuzziness; }
            set {
                if (value != _maskBrushFuzziness) {
                    _maskBrushFuzziness = value;
                }
            }
        }

        [SerializeField, Range(0, 1)]
        float _maskBrushOpacity = 0.25f;

        public float maskBrushOpacity {
            get { return _maskBrushOpacity; }
            set {
                if (value != _maskBrushOpacity) {
                    _maskBrushOpacity = value;
                }
            }
        }


        #endregion


        // internal fields
        struct ExcludedRenderer {
            public Renderer renderer;
            public bool wasVisible;
        }


        [NonSerialized]
        public Material composeMat, distantSnowMat;
        Material decalMat, snowParticle, snowParticleIllum, snowParticleIllumOpaque, blurMat;
        GameObject snowCamObj;
        Camera cameraEffect, snowCam;
        static Camera zenithCam;
        RenderTexture depthTexture, snowedSceneTexture, snowedSceneTexture2, footprintTexture, footprintTexture2, decalTexture, decalTexture2;
        Shader depthShader, depthShaderMask;
        Vector3 lastCameraEffectPosition, lastCameraMarkPosition;
        Shader snowShader, eraseShader;
        Texture2D snowTex, snowNormalsTex, noiseTex;
        int lastPosX, lastPosZ;
        Vector3 lastTargetPos;
        [NonSerialized]
        public ParticleSystem snowfallSystem;
        float snowfallSystemAltitude = 50f;
        float lastSnowfallIntensity = float.MaxValue;
        float lastSnowfallDistance;
        ParticleSystem.Particle[] m_Particles;
        List<GlobalSnowIgnoreCoverage> ignoredGOs = new List<GlobalSnowIgnoreCoverage>();
        int currentCoverageResolution, currentCoverageExtension;
        bool needUpdateSnowCoverage, needFootprintBlit;
        List<Vector4> decalRequests = new List<Vector4>();
        float lastFootprintRemovalTime, lastMarkRemovalTime;
        Shader snowedSpeedTreeBillboardShader;
        Quaternion lastSunRotation;
        bool sunOccluded;
        List<string> distanceMatKeywords = new List<string>();
        List<string> composeMatKeywords = new List<string>();
        MaterialPropertyBlock terrainMatPropBlock;
        bool needsUpdateProperties;
        RenderingPath lastRenderingPath;
        bool lastCameraAllowHDR;
        CommandBuffer buf;
        Material cbMat, cbMatExcludedObjects;
        Mesh decalMesh;
        DEFERRED_CAMERA_EVENT currentCameraEvent = DEFERRED_CAMERA_EVENT.BeforeReflections;
        bool needRebuildCommandBuffer, performFullSceneScan;
        Vector4[] targetUVArray;
        Vector4[] worldPosArray;
        bool tempIgnorePrecull; // used to capture e ignore Shift key during OnPrecull at Editor time which caused issues editing object names

        ExcludedRenderer[] excludedRenderers;
        int excludedRenderersCount;
        Dictionary<Renderer, int> excludedRenderersDict;

        List<GameObject> sceneGameRootGameObjects;
        List<Renderer> sceneRenderers, tmpRenderers;
        int normalsID, diffuseID, specularID, maskTargetID;
        RenderTargetIdentifier[] mrtFlatHDR, mrtFlatNonHDR, mrtHDR, mrtNonHDR;

        bool requestedFootprint;
        Vector3 requestedFootprintPos, requestedFootprintDir;
        int effectStartFrameCount;

        #region Game loop events

        void OnEnable() {

            cameraEffect = gameObject.GetComponent<Camera>();
            normalsID = Shader.PropertyToID("_GS_GBuffer2Copy");
            diffuseID = Shader.PropertyToID("_GS_GBuffer0Copy");
            specularID = Shader.PropertyToID("_GS_GBuffer1Copy");
            maskTargetID = Shader.PropertyToID("_GS_DeferredExclusionBuffer");

            mrtFlatHDR = new RenderTargetIdentifier[] {
                                                                                                                                BuiltinRenderTextureType.GBuffer0,    // Albedo, Occ
                                                                                                                                BuiltinRenderTextureType.GBuffer1,    // Spec, Roughness
                                                                                                                                BuiltinRenderTextureType.CameraTarget // Emission / Ambient light
                                                                                                                };
            mrtFlatNonHDR = new RenderTargetIdentifier[] {
                                                                                                                                BuiltinRenderTextureType.GBuffer0,    // Albedo, Occ
                                                                                                                                BuiltinRenderTextureType.GBuffer1,    // Spec, Roughness
                                                                                                                                BuiltinRenderTextureType.GBuffer3    // Emission / Ambient light
            };
            mrtHDR = new RenderTargetIdentifier[] {
                                                                                                                                BuiltinRenderTextureType.GBuffer0,    // Albedo, Occ
                                                                                                                                BuiltinRenderTextureType.GBuffer1,    // Spec, Roughness
                                                                                                                                BuiltinRenderTextureType.GBuffer2,     // Normals
                                                                                                                                BuiltinRenderTextureType.CameraTarget // Emission / Ambient light
                                                                                                                };
            mrtNonHDR = new RenderTargetIdentifier[] {
                                                                                                                                BuiltinRenderTextureType.GBuffer0,    // Albedo, Occ
                                                                                                                                BuiltinRenderTextureType.GBuffer1,    // Spec, Roughness
                                                                                                                                BuiltinRenderTextureType.GBuffer2,     // Normals
                                                                                                                                BuiltinRenderTextureType.GBuffer3    // Emission / Ambient light
                                                                                                                };

            effectStartFrameCount = Time.frameCount;
            Transform t = transform;
            while (t.parent != null) {
                t = t.parent;
            }
            _characterController = t.GetComponentInChildren<CharacterController>();
            needFootprintBlit = true; // forces blit footprints if enabled

            lastCameraEffectPosition = Vector3.one * 1000;
            SetupSun();
            UpdatePropertiesNow();
            needsUpdateProperties = true;

#if UNITY_EDITOR
            RefreshSceneView();
#endif
        }

        void LoadResources() {

            if (composeMat == null) {
                composeMat = Instantiate(Resources.Load<Material>("Common/Materials/Compose"));
                composeMat.hideFlags = HideFlags.DontSave;
            }

            RemoveCommandBuffer();
            if (deferred) {
                performFullSceneScan = true;
                RebuildCommandBuffer();
                CameraEvent cameraEvent = _deferredCameraEvent.ToUnityCameraEvent();
                cameraEffect.AddCommandBuffer(cameraEvent, buf);
                currentCameraEvent = _deferredCameraEvent;
            } else {

                snowShader = Shader.Find("GlobalSnow/Snow");
                if (snowShader == null) {
                    if (_forceForwardRenderingPath) {
                        forceForwardRenderingPath = false;
                        return;
                    }
                    Debug.LogError("Snow shader not found. Workflow_Forward folder under Resources might be removed?");
                    enabled = false;
                    return;
                }
                if (!snowShader.isSupported) {
                    Debug.Log("Snow shader not supported on this platform.");
                    enabled = false;
                    return;
                }

                if (eraseShader == null) {
                    eraseShader = Shader.Find("GlobalSnow/EraseShader");
                    if (!eraseShader.isSupported) {
                        Debug.Log("EraseShader shader not supported on this platform.");
                        enabled = false;
                        return;
                    }
                }
                if (snowedSpeedTreeBillboardShader == null) {
                    snowedSpeedTreeBillboardShader = Shader.Find("GlobalSnow/SpeedTree Billboard Adapted");
                }
                if (distantSnowMat == null) {
                    distantSnowMat = Instantiate(Resources.Load("Workflow_Forward/Materials/GlobalSnowDistant")) as Material;
                    distantSnowMat.hideFlags = HideFlags.DontSave;
                }
            }

            if (depthShader == null) {
                depthShader = Shader.Find("GlobalSnow/DepthCopy");
                if (!depthShader.isSupported) {
                    Debug.Log("Depth shader not supported on this platform.");
                    enabled = false;
                    return;
                }
            }

            if (depthShaderMask == null) {
                depthShaderMask = Shader.Find("GlobalSnow/DepthCopyMask");
                if (!depthShaderMask.isSupported) {
                    Debug.Log("Depth Mask shader not supported on this platform.");
                    enabled = false;
                    return;
                }
            }

            if (decalMat == null) {
                Shader decalShader = Shader.Find("GlobalSnow/DecalDraw");
                if (!decalShader.isSupported) {
                    Debug.Log("Decal Drawing not supported on this platform.");
                    enabled = false;
                    return;
                }
                decalMat = new Material(decalShader);
                decalMat.hideFlags = HideFlags.DontSave;
            }

            if (blurMat == null) {
                Shader blurShader = Shader.Find("GlobalSnow/DepthBlur");
                if (!blurShader.isSupported) {
                    Debug.Log("Blur not supported on this platform.");
                    enabled = false;
                    return;
                }
                blurMat = new Material(blurShader);
                blurMat.hideFlags = HideFlags.DontSave;
            }

            if (snowTex == null) {
                snowTex = Resources.Load<Texture2D>("Common/Textures/Snow");
            }
            if (noiseTex == null) {
                noiseTex = Resources.Load<Texture2D>("Common/Textures/Noise5");
            }
            if (snowNormalsTex == null) {
                snowNormalsTex = Resources.Load<Texture2D>("Common/Textures/Noise5Normals");
            }
            if (snowParticle == null) {
                snowParticle = Resources.Load<Material>("Common/Materials/SnowParticle");
            }
            if (snowParticleIllum == null) {
                snowParticleIllum = Resources.Load<Material>("Common/Materials/SnowParticleIllum");
            }
            if (snowParticleIllumOpaque == null) {
                snowParticleIllumOpaque = Resources.Load<Material>("Common/Materials/SnowParticleIllumOpaque");
            }

            // Set global textures for replacement shader
            Shader.SetGlobalTexture("_GS_SnowTex", snowTex);
            Shader.SetGlobalTexture("_GS_SnowNormalsTex", snowNormalsTex);
            Shader.SetGlobalTexture("_GS_NoiseTex", noiseTex);
        }


        void RebuildCommandBuffer() {
            needRebuildCommandBuffer = false;
            if (cbMat == null) {
                cbMat = Resources.Load<Material>("Workflow_Deferred/Materials/DeferredSnow");
                if (cbMat == null) {
                    forceForwardRenderingPath = true;
                    return;
                }
            }

            if (buf == null) {
                buf = new CommandBuffer();
                buf.name = "Deferred Snow";
            } else {
                buf.Clear();
            }

            if (cbMatExcludedObjects == null) {
                cbMatExcludedObjects = new Material(Shader.Find("GlobalSnow/DeferredMaskWrite"));
            }


            RenderTextureDescriptor rtDesc;
#if UNITY_2017_2_OR_NEWER
            if (UnityEngine.XR.XRSettings.enabled) {
                rtDesc = UnityEngine.XR.XRSettings.eyeTextureDesc;
            } else {
                rtDesc = new RenderTextureDescriptor(cameraEffect.pixelWidth, cameraEffect.pixelHeight);
            }
#else
            rtDesc = new RenderTextureDescriptor(cameraEffect.pixelWidth, cameraEffect.pixelHeight);
#endif
            rtDesc.depthBufferBits = 0;
            rtDesc.sRGB = false;
            rtDesc.msaaSamples = 1;
            rtDesc.useMipMap = false;
            rtDesc.volumeDepth = 1;

            // Copy normals
            bool flatShading = _snowQuality == SNOW_QUALITY.FlatShading;
            if (!flatShading) {
                RenderTextureDescriptor normalsDesc = rtDesc;
                if (_floatingPointNormalsBuffer && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) {
                    normalsDesc.colorFormat = RenderTextureFormat.ARGBHalf;
                }
                buf.GetTemporaryRT(normalsID, normalsDesc);
                buf.Blit(BuiltinRenderTextureType.GBuffer2, normalsID);
            }
            // Copy diffuse
            buf.GetTemporaryRT(diffuseID, rtDesc);
            buf.Blit(BuiltinRenderTextureType.GBuffer0, diffuseID);
            // Copy specular
            buf.GetTemporaryRT(specularID, rtDesc);
            buf.Blit(BuiltinRenderTextureType.GBuffer1, specularID);

            // Draw excluded objects
            RenderTextureDescriptor rtMaskDesc = rtDesc;
            rtMaskDesc.colorFormat = RenderTextureFormat.Depth;
            rtMaskDesc.depthBufferBits = 24;

            buf.GetTemporaryRT(maskTargetID, rtMaskDesc);
            buf.SetRenderTarget(maskTargetID);
            buf.ClearRenderTarget(true, false, Color.white);

            if (excludedRenderersDict == null) {
                excludedRenderersDict = new Dictionary<Renderer, int>();
            }

            ExcludedRenderer excludedRenderer = new ExcludedRenderer();

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) {
                needRebuildCommandBuffer = true;
                performFullSceneScan = true;
            } else {
                // Get all renderers not included in snow layer mask
                if (performFullSceneScan) {
                    FindExcludedRenderersInScene(scene);
                }

                // Exclude objects using GlobalSnowIgnoreCoverate script
                int count = ignoredGOs.Count;
                for (int k = 0; k < count; k++) {
                    GlobalSnowIgnoreCoverage ic = ignoredGOs[k];
                    if (ic != null && ic.isActiveAndEnabled && ic.renderers != null) {
                        int rrCount = ic.renderers.Length;
                        for (int j = 0; j < rrCount; j++) {
                            Renderer r = ic.renderers[j];
                            if (r.enabled && r.gameObject.activeInHierarchy) {
                                int index;
                                if (!excludedRenderersDict.TryGetValue(r, out index)) {
                                    excludedRenderer.renderer = r;
                                    excludedRenderer.wasVisible = !ic.receiveSnow && r.enabled && r.gameObject.activeInHierarchy;
                                    GrowIfNeeded(ref excludedRenderers, excludedRenderersCount);
                                    excludedRenderers[excludedRenderersCount] = excludedRenderer;
                                    excludedRenderersDict[r] = excludedRenderersCount;
                                    excludedRenderersCount++;
                                } else if (ic.receiveSnow) {
                                    excludedRenderers[index].renderer = null;
                                    excludedRenderersDict.Remove(r);
                                }
                            }
                        }
                    }
                }

                // Exclude scene objects not in layer mask
                for (int k = 0; k < excludedRenderersCount; k++) {
                    Renderer r = excludedRenderers[k].renderer;
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy) {
                        AddRendererToCommandBuffer(r, cbMatExcludedObjects);
                    }
                }
            }


            RenderTargetIdentifier[] mrt;
            if (flatShading) {
                if (cameraEffect.allowHDR) {
                    mrt = mrtFlatHDR;
                } else {
                    mrt = mrtFlatNonHDR;
                }
            } else {
                if (cameraEffect.allowHDR) {
                    mrt = mrtHDR;
                } else {
                    mrt = mrtNonHDR;
                }
            }
            buf.SetRenderTarget(mrt, BuiltinRenderTextureType.CameraTarget);

            if (decalMesh == null) {
                decalMesh = new Mesh { name = "GS Fullscreen Triangle" };
                decalMesh.SetVertices(new List<Vector3> {
                                                                                                                new Vector3 (-1f, -1f, 0f),
                                                                                                                new Vector3 (-1f, 3f, 0f),
                                                                                                                new Vector3 (3f, -1f, 0f)
                                                                                                });
                decalMesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
                decalMesh.UploadMeshData(false);

            }
            buf.DrawMesh(decalMesh, Matrix4x4.identity, cbMat, 0, flatShading ? 1 : 0);

            buf.ReleaseTemporaryRT(maskTargetID);

            if (!flatShading) {
                buf.ReleaseTemporaryRT(normalsID);
            }
            buf.ReleaseTemporaryRT(diffuseID);
            buf.ReleaseTemporaryRT(specularID);

            lastCameraAllowHDR = cameraEffect.allowHDR;
        }

        void GrowIfNeeded<T>(ref T[] array, int index) {
            if (index >= array.Length) {
                T[] newArray = new T[index * 2];
                Array.Copy(array, newArray, array.Length);
                array = newArray;
            }
        }


        void AddRendererToCommandBuffer(Renderer r, Material mat) {
            int subMeshCount = 0;
            if (r is SkinnedMeshRenderer) {
                SkinnedMeshRenderer sm = (SkinnedMeshRenderer)r;
                if (sm.sharedMesh != null) {
                    subMeshCount = sm.sharedMesh.subMeshCount;
                }
            } else if (r is MeshRenderer) {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) {
                    subMeshCount = mf.sharedMesh.subMeshCount;
                }
            }

            for (int l = 0; l < subMeshCount; l++) {
                buf.DrawRenderer(r, mat, l);
            }
        }

        void RemoveCommandBuffer() {
            if (buf != null && cameraEffect != null) {
                CameraEvent cameraEvent = currentCameraEvent.ToUnityCameraEvent();
                cameraEffect.RemoveCommandBuffer(cameraEvent, buf);
                buf = null;
            }
        }

        void FindExcludedRenderersInScene(Scene scene) {
            if (Time.frameCount >= effectStartFrameCount) { // repeat scan next frames so it gives time to initialization/load of other objects
                performFullSceneScan = false;
            }

            if (excludedRenderers == null) {
                excludedRenderers = new ExcludedRenderer[64];
            }
            excludedRenderersCount = 0;

            if (sceneGameRootGameObjects == null) {
                sceneGameRootGameObjects = new List<GameObject>(256);
            } else {
                sceneGameRootGameObjects.Clear();
            }
            if (tmpRenderers == null) {
                tmpRenderers = new List<Renderer>();
            } else {
                tmpRenderers.Clear();
            }
            if (sceneRenderers == null) {
                sceneRenderers = new List<Renderer>();
            } else {
                sceneRenderers.Clear();
            }
            excludedRenderersDict.Clear();

            scene.GetRootGameObjects(sceneGameRootGameObjects);
            int count = sceneGameRootGameObjects.Count;
            for (int k = 0; k < count; k++) {
                GameObject o = sceneGameRootGameObjects[k];
                o.GetComponentsInChildren(true, tmpRenderers);
                sceneRenderers.AddRange(tmpRenderers);
            }

            ExcludedRenderer excludedRenderer = new ExcludedRenderer();
            count = sceneRenderers.Count;
            for (int k = 0; k < count; k++) {
                Renderer r = sceneRenderers[k];
                if (r.enabled && r.gameObject.activeInHierarchy) {
                    int objLayer = r.gameObject.layer;
                    if (objLayer != 0 && objLayer != SNOW_PARTICLES_LAYER && (objLayer == _defaultExclusionLayer || ((1 << objLayer) & _layerMask.value) == 0)) {
                        excludedRenderer.renderer = r;
                        excludedRenderer.wasVisible = r.isVisible || Time.frameCount < 5;
                        GrowIfNeeded(ref excludedRenderers, excludedRenderersCount);
                        excludedRenderersDict[r] = excludedRenderersCount;
                        excludedRenderers[excludedRenderersCount] = excludedRenderer;
                        excludedRenderersCount++;
                    }
                }
            }
        }



        void Reset() {
            SetupSun();
            UpdateProperties();
        }

        void OnDisable() {
            RemoveCommandBuffer();
            UpdateSnowData2(999999);
            GlobalSnowImageEffect ef = GetComponent<GlobalSnowImageEffect>();
            if (ef != null) {
                ef.enabled = false;
            }
        }

#if UNITY_EDITOR
        void RefreshSceneView() {
            // Snow is lost in Scene Camera - refresh it
            if (UnityEditor.SceneView.lastActiveSceneView != null) {
                Camera sceneCam = UnityEditor.SceneView.lastActiveSceneView.camera;
                if (sceneCam != null) {
                    GlobalSnow sceneSnow = sceneCam.GetComponent<GlobalSnow>();
                    if (sceneSnow != null) {
                        sceneSnow.UpdateProperties();
                        sceneSnow.needsUpdateProperties = true;
                    }
                }

            }
        }
#endif

        void OnDestroy() {
            CleanUpTextureDepth();
            CleanUpTextureSnowScene();
            if (snowCamObj != null) {
                DestroyImmediate(snowCamObj);
                snowCamObj = null;
            }
            if (footprintTexture != null) {
                footprintTexture.Release();
                footprintTexture = null;
            }
            if (footprintTexture2 != null) {
                footprintTexture2.Release();
                footprintTexture2 = null;
            }
            if (decalTexture != null) {
                decalTexture.Release();
                decalTexture = null;
            }
            if (decalTexture2 != null) {
                decalTexture2.Release();
                decalTexture2 = null;
            }
            if (snowfallSystem != null) {
                DestroyImmediate(snowfallSystem.gameObject);
                snowfallSystem = null;
            }
        }


        void UpdateSnowCoverageNow() {

            needUpdateSnowCoverage = false;
            const float camAltitudeOffset = 100f;
            int res = (int)Mathf.Pow(2, _coverageResolution + 8);

            // Setup zenith cam
            if (currentCoverageResolution != _coverageResolution || currentCoverageExtension != _coverageExtension) {
                if (zenithCam != null) {
                    DestroyImmediate(zenithCam.gameObject);
                    zenithCam = null;
                    CleanUpTextureDepth();
                }
            }
            if (zenithCam == null) {
                GameObject camGO = GameObject.Find(ZENITH_CAM);
                if (camGO == null) {
                    camGO = new GameObject(ZENITH_CAM);
                    zenithCam = camGO.AddComponent<Camera>();
                } else {
                    zenithCam = camGO.GetComponent<Camera>();
                }
                camGO.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                zenithCam.enabled = false;
                zenithCam.renderingPath = RenderingPath.Forward;
                zenithCam.orthographic = true;
                currentCoverageResolution = _coverageResolution;
                currentCoverageExtension = _coverageExtension;
            }
            zenithCam.orthographicSize = Mathf.Pow(2, 7f + _coverageExtension);
            zenithCam.clearFlags = CameraClearFlags.SolidColor;
            zenithCam.allowMSAA = false;
            zenithCam.backgroundColor = new Color(1f, 0f, 0f, 0f); 
            zenithCam.cullingMask = zenithalMask & ~(1 << SNOW_PARTICLES_LAYER) & ~(1 << _defaultExclusionLayer);
            Vector3 currentSnowCamPosition = new Vector3(cameraEffect.transform.position.x, cameraEffect.transform.position.y + camAltitudeOffset, cameraEffect.transform.position.z);
            zenithCam.nearClipPlane = 1f;
            zenithCam.farClipPlane = Mathf.Max(currentSnowCamPosition.y - _minimumAltitude, 0.01f) + 1f;
            zenithCam.transform.position = currentSnowCamPosition;
            zenithCam.transform.rotation = Quaternion.Euler(90, 0, 0);
            // Render from above
            if (depthTexture == null) {
                depthTexture = new RenderTexture(res, res, 24, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear);
                depthTexture.hideFlags = HideFlags.DontSave;
                depthTexture.filterMode = FilterMode.Bilinear;
                depthTexture.wrapMode = TextureWrapMode.Clamp;
                depthTexture.Create();
            }
            zenithCam.targetTexture = depthTexture;
            if (_coverageMask && _coverageMaskTexture != null) {
                zenithCam.RenderWithShader(depthShaderMask, null);
            } else {
                zenithCam.RenderWithShader(depthShader, null);
            }

            if (_smoothCoverage && Camera.current.cameraType != CameraType.SceneView) {
                // This option prevents object selection in SceneView on Unity 2019.3 (seems like an Unity bug)
                RenderTexture rt1 = RenderTexture.GetTemporary(res, res, 0, depthTexture.format, RenderTextureReadWrite.Linear);
                Graphics.Blit(depthTexture, rt1, blurMat, 0);
                depthTexture.DiscardContents();
                Graphics.Blit(rt1, depthTexture, blurMat, 1);
                RenderTexture.ReleaseTemporary(rt1);
            }

            Shader.SetGlobalTexture("_GS_DepthTexture", depthTexture);
            Shader.SetGlobalVector("_GS_SnowCamPos", new Vector4(zenithCam.transform.position.x, zenithCam.transform.position.y, zenithCam.transform.position.z, zenithCam.farClipPlane));
        }

        void RenderFootprints() {
            if (footprintTexture == null || footprintTexture2 == null) {
                footprintTexture = new RenderTexture(FOOTPRINT_TEXTURE_RESOLUTION, FOOTPRINT_TEXTURE_RESOLUTION, 0, RenderTextureFormat.ARGBFloat);
                footprintTexture.filterMode = FilterMode.Point;
                footprintTexture.wrapMode = TextureWrapMode.Repeat;
                footprintTexture.hideFlags = HideFlags.DontSave;
                footprintTexture2 = new RenderTexture(FOOTPRINT_TEXTURE_RESOLUTION, FOOTPRINT_TEXTURE_RESOLUTION, 0, RenderTextureFormat.ARGBFloat);
                footprintTexture2.filterMode = FilterMode.Point;
                footprintTexture2.wrapMode = TextureWrapMode.Repeat;
                footprintTexture2.hideFlags = HideFlags.DontSave;
                Shader.SetGlobalTexture("_GS_FootprintTex", footprintTexture2);
            }
            Vector3 targetPos = requestedFootprint ? requestedFootprintPos : cameraEffect.transform.position;
            targetPos.x *= 1f / _footprintsScale;
            targetPos.z *= 1f / _footprintsScale;
            int posX = Mathf.FloorToInt(targetPos.x);
            int posZ = Mathf.FloorToInt(targetPos.z);
            bool doBlit = false;
            if (requestedFootprint || (_footprintsAutoFPS && (posX != lastPosX || posZ != lastPosZ || needFootprintBlit))) {
                Vector2 fracPos = new Vector2(targetPos.x - posX, targetPos.z - posZ);
                bool validStep = (lastPosX != posX && lastPosZ != posZ);
                if (!validStep && fracPos.x > 0.25f && fracPos.x < 0.75f && fracPos.y >= 0.25f && fracPos.y < 0.75f) {
                    validStep = true;
                }
                // check correct path
                if (!validStep) {
                    Vector2 vdir = new Vector2(targetPos.x - lastTargetPos.x, targetPos.z - lastTargetPos.z).normalized;
                    Vector2 tdir = new Vector2(posX - lastPosX, posZ - lastPosZ).normalized;
                    const float angleThreshold = 0.9f;
                    if (Vector2.Dot(vdir, tdir) > angleThreshold) {
                        validStep = true;
                    }
                }
                if (validStep) {
                    // check player is grounded
                    if (_groundCheck == GROUND_CHECK.RayCast) {
                        validStep = Physics.Linecast(targetPos, new Vector3(targetPos.x, targetPos.y - _groundDistance, targetPos.z));
                    } else if (_groundCheck == GROUND_CHECK.CharacterController && _characterController != null) {
                        validStep = _characterController.isGrounded;
                    }
                }
                if (validStep || needFootprintBlit) {
                    Vector3 targetDir = requestedFootprint ? requestedFootprintDir : cameraEffect.transform.forward;
                    Vector2 dirxz = new Vector2(targetDir.x, targetDir.z).normalized;
                    float angle = ((UnityEngine.Random.value - 0.5f) * 8f - GetAngle(Vector2.up, dirxz)) * Mathf.Deg2Rad;
                    Vector2 vpos = new Vector2(((posX + FOOTPRINT_TEXTURE_RESOLUTION) % FOOTPRINT_TEXTURE_RESOLUTION) + 0.5f, ((posZ + FOOTPRINT_TEXTURE_RESOLUTION) % FOOTPRINT_TEXTURE_RESOLUTION) + 0.5f);
                    Vector4 targetUV = new Vector4(vpos.x / FOOTPRINT_TEXTURE_RESOLUTION, vpos.y / FOOTPRINT_TEXTURE_RESOLUTION, Mathf.Cos(angle), Mathf.Sin(angle));
                    decalMat.SetVector("_TargetUV", targetUV);
                    if (zenithCam == null) {
                        decalMat.SetVector("_WorldPos", new Vector3(targetPos.x, 0, targetPos.z));
                    } else {
                        decalMat.SetVector("_WorldPos", new Vector3(posX, (zenithCam.transform.position.y - targetPos.y) / zenithCam.farClipPlane, posZ));
                    }
                    float decalRadius = 0.5f / FOOTPRINT_TEXTURE_RESOLUTION;
                    if (validStep) {
                        lastPosX = posX;
                        lastPosZ = posZ;
                        lastTargetPos = targetPos;
                    } else {
                        decalRadius = 0f;
                    }
                    decalMat.SetFloat("_DrawDist", decalRadius);
                    doBlit = true;
                    needFootprintBlit = false;
                }
            }
            float dTime = Time.time - lastFootprintRemovalTime;
            if (dTime > _footprintsDuration / 200f) {
                lastFootprintRemovalTime = Time.time;
                decalMat.SetFloat("_EraseSpeed", Mathf.Max(dTime / _footprintsDuration, 1 / 200f));
                if (!doBlit) {
                    decalMat.SetFloat("_DrawDist", 0);
                    doBlit = true;
                }
            } else {
                decalMat.SetFloat("_EraseSpeed", 0);
            }
            if (doBlit || !Application.isPlaying) {
                Graphics.Blit(footprintTexture, footprintTexture2, decalMat, 0);
                // Flip decal buffer
                RenderTexture decalAux = footprintTexture;
                footprintTexture = footprintTexture2;
                footprintTexture2 = decalAux;
            }
            requestedFootprint = false;
        }


        void RenderTerrainMarks() {
            int textureSize = (int)terrainMarksTextureSize;
            bool usesHighPrecisionTexture = !Application.isMobilePlatform || QualitySettings.activeColorSpace == ColorSpace.Linear;
            if (decalTexture == null || decalTexture2 == null) {
                RenderTextureFormat rtFormat = usesHighPrecisionTexture ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32;
                decalTexture = new RenderTexture(textureSize, textureSize, 0, rtFormat);
                decalTexture.wrapMode = TextureWrapMode.Repeat;
                decalTexture.hideFlags = HideFlags.DontSave;
                decalTexture2 = new RenderTexture(textureSize, textureSize, 0, rtFormat);
                decalTexture2.wrapMode = TextureWrapMode.Repeat;
                decalTexture2.hideFlags = HideFlags.DontSave;
                // Clean textures
                Graphics.Blit(decalTexture, decalTexture2, decalMat, 2);
                RenderTexture decalAux = decalTexture;
                decalTexture = decalTexture2;
                decalTexture2 = decalAux;
                Graphics.Blit(decalTexture, decalTexture2, decalMat, 2);
                decalAux = decalTexture;
                decalTexture = decalTexture2;
                decalTexture2 = decalAux;
            }

            bool needCleanBlit = false;
            float dTime = Time.time - lastMarkRemovalTime;
            float eraseSpeed = 0;
            if (dTime > _terrainMarksDuration / 200f) {
                lastMarkRemovalTime = Time.time;
                eraseSpeed = Mathf.Max(dTime / _terrainMarksDuration, 1 / 200f);
                needCleanBlit = true;
            }
            decalMat.SetFloat("_EraseSpeed", eraseSpeed);

            const int MAX_DECAL_BLITS_PER_FRAME = 4;
            const float scale = 8f;
            const int VECTOR_ARRAY_LENGTH = 64;

            if (targetUVArray == null || targetUVArray.Length == 0) {
                targetUVArray = new Vector4[VECTOR_ARRAY_LENGTH];
                worldPosArray = new Vector4[VECTOR_ARRAY_LENGTH];
            }
            int drCount = decalRequests.Count;
            for (int blit = 0; blit < MAX_DECAL_BLITS_PER_FRAME; blit++) {
                int stamps = 0;
                for (int k = 0; k < VECTOR_ARRAY_LENGTH; k++) {
                    if (drCount == 0) {
                        targetUVArray[k] = Vector4.zero;
                        worldPosArray[k] = Vector4.zero;
                        continue;
                    }
                    drCount--;
                    Vector4 vpos = decalRequests[drCount];
                    decalRequests.RemoveAt(drCount);

                    if (_terrainMarksRoofMinDistance > 0) {
                        Vector3 wpos = vpos;
                        wpos.y += _terrainMarksRoofMinDistance;
                        Ray ray = new Ray(wpos, Vector3.up);
                        if (Physics.Raycast(ray)) continue;
                    }

                    float drawDist = vpos.w * scale / textureSize;
                    targetUVArray[k] = new Vector4((((vpos.x + textureSize) * scale) % textureSize) / textureSize,
                                                   (((vpos.z + textureSize) * scale) % textureSize) / textureSize, 0, drawDist * drawDist);

                    if (zenithCam == null) {
                        worldPosArray[stamps] = new Vector4(vpos.x, 0, vpos.z, 0);
                    } else {
                        worldPosArray[stamps] = new Vector4(vpos.x, (zenithCam.transform.position.y - vpos.y) / zenithCam.farClipPlane, vpos.z, 0);
                    }
                    stamps++;
                }
                if (stamps == 0) break;

                decalMat.SetInt("_TargetCount", stamps);
                decalMat.SetVectorArray("_TargetUVArray", targetUVArray);
                decalMat.SetVectorArray("_WorldPosArray", worldPosArray);

                Graphics.Blit(decalTexture, decalTexture2, decalMat, 1);

                // Flip decal buffer
                RenderTexture decalAux = decalTexture;
                decalTexture = decalTexture2;
                decalTexture2 = decalAux;
                needCleanBlit = false;
                decalMat.SetFloat("_EraseSpeed", 0); // don't erase anymore for this frame
            }

            if (needCleanBlit) {
                Graphics.Blit(decalTexture, decalTexture2, decalMat, 3);
                // Flip decal buffer
                RenderTexture decalAux = decalTexture;
                decalTexture = decalTexture2;
                decalTexture2 = decalAux;
            }
            Shader.SetGlobalTexture("_GS_DecalTex", decalTexture);
        }


        void SnowRender_DeferredPath() {

            // Render footprints
            if (_footprints && decalMat != null)
                RenderFootprints();

            // Render decals
            if (_terrainMarks && decalMat != null)
                RenderTerrainMarks();


        }

        void SnowRender_ForwardPath() {

            // Render footprints
            if (_footprints && decalMat != null)
                RenderFootprints();

            // Render decals
            if (_terrainMarks && decalMat != null)
                RenderTerrainMarks();

            // Render snowed scene
            CleanUpTextureSnowScene();
            if (snowCam == null) {
                if (snowCamObj == null) {
                    snowCamObj = new GameObject(REP_CAM);
                    snowCamObj.hideFlags = HideFlags.HideAndDontSave;
                    snowCam = snowCamObj.AddComponent<Camera>();
                    snowCam.enabled = false;
                } else {
                    snowCam = snowCamObj.GetComponent<Camera>();
                    if (snowCam == null) {
                        DestroyImmediate(snowCamObj);
                        snowCamObj = null;
                        return;
                    }
                }
            }

            snowCam.CopyFrom(cameraEffect);
            if (_forceSPSR && UnityEngine.XR.XRSettings.enabled && Application.isPlaying) {
                snowCam.SetStereoViewMatrix(Camera.StereoscopicEye.Left, cameraEffect.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
                snowCam.projectionMatrix = cameraEffect.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            }
            snowCam.renderingPath = RenderingPath.Forward;
            snowCam.depthTextureMode = DepthTextureMode.None;
            snowedSceneTexture = RenderTexture.GetTemporary(snowCam.pixelWidth, snowCam.pixelHeight, 24, RenderTextureFormat.ARGB32);
            snowCam.backgroundColor = new Color(0, 0, 0, 0);
            snowCam.clearFlags = CameraClearFlags.SolidColor;
            snowCam.allowMSAA = false;
            snowCam.allowHDR = false;
            snowCam.targetTexture = snowedSceneTexture;
            snowCam.rect = new Rect(0, 0, 1f, 1f);
            LayerMask layerMask;
#if UNITY_EDITOR
            GlobalSnow realcam = GlobalSnow.instance;
            layerMask = GlobalSnow.instance.layerMask;
#else
												layerMask = _layerMask;
#endif
            int cameraCullingMask = _excludedCastShadows ? cameraEffect.cullingMask : cameraEffect.cullingMask & layerMask;
            if (_snowfall && _snowfallReceiveShadows) {
                snowCam.cullingMask = cameraCullingMask; // don't include layermask so everyone can cast shadows
            } else {
                snowCam.cullingMask = cameraCullingMask & ~(1 << SNOW_PARTICLES_LAYER); // don't include layermask so everyone can cast shadows
            }
            if (_distanceOptimization) {
                snowCam.farClipPlane = _detailDistance;
            }
            if (_snowQuality == SNOW_QUALITY.ReliefMapping) {
                if (terrainMatPropBlock == null) {
                    terrainMatPropBlock = new MaterialPropertyBlock();
                    terrainMatPropBlock.SetFloat("_GS_ReliefFlag", 1f);
                }
                Terrain[] terrains = Terrain.activeTerrains;
                for (int k = 0; k < terrains.Length; k++) {
                    terrains[k].SetSplatMaterialPropertyBlock(terrainMatPropBlock);
                }
            }
            snowCam.RenderWithShader(snowShader, "RenderType");

            if (layerMask.value != -1 || ignoredGOs.Count > 0) {
                snowCam.cullingMask = cameraEffect.cullingMask & ((1 << _defaultExclusionLayer) | ~layerMask.value) & ~(1 << SNOW_PARTICLES_LAYER);
                snowCam.clearFlags = CameraClearFlags.Nothing;
                snowCam.RenderWithShader(eraseShader, "RenderType");
            }

            // Pass result to main
            composeMat.SetTexture("_SnowedScene", snowedSceneTexture);

            // Right eye
#if UNITY_5_5_OR_NEWER
            if (_forceSPSR && UnityEngine.XR.XRSettings.enabled && Application.isPlaying) {
                snowCam.SetStereoViewMatrix(Camera.StereoscopicEye.Right, cameraEffect.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right));
                snowCam.projectionMatrix = cameraEffect.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

                snowedSceneTexture2 = RenderTexture.GetTemporary(snowCam.pixelWidth, snowCam.pixelHeight, 24, RenderTextureFormat.ARGB32);
                snowCam.targetTexture = snowedSceneTexture2;
                snowCam.clearFlags = CameraClearFlags.SolidColor;
                if (_snowfall && _snowfallReceiveShadows) {
                    snowCam.cullingMask = cameraCullingMask; // don't include layermask so everyone can cast shadows
                } else {
                    snowCam.cullingMask = cameraCullingMask & ~(1 << SNOW_PARTICLES_LAYER); // don't include layermask so everyone can cast shadows
                }
                snowCam.projectionMatrix = snowCam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

                snowCam.RenderWithShader(snowShader, "RenderType");
                if (layerMask.value != -1 || ignoredGOs.Count > 0) {
                    snowCam.cullingMask = cameraEffect.cullingMask & ((1 << _defaultExclusionLayer) | ~layerMask.value) & ~(1 << SNOW_PARTICLES_LAYER);
                    snowCam.clearFlags = CameraClearFlags.Nothing;
                    snowCam.RenderWithShader(eraseShader, "RenderType");
                }
                composeMat.SetTexture("_SnowedScene2", snowedSceneTexture2);
            }
#endif

        }

        // Do the magic
        void OnPreCull() {

            GlobalSnow gs = GlobalSnow.instance;
            if (!enabled || !gameObject.activeInHierarchy || cameraEffect == null || gs == null)
                return;

            if (needsUpdateProperties) {
                needsUpdateProperties = false;
                UpdatePropertiesNow();
            }

            CheckSunOcclusion();

#if UNITY_EDITOR
            if (!Application.isPlaying) {
                Event e = Event.current;
                if (tempIgnorePrecull) {
                    tempIgnorePrecull = false;
                    return;
                }
                if (e != null && e.shift) {
                    tempIgnorePrecull = true;
                    return;
                }
            }
#endif


            // Apply exclusion list
            List<GlobalSnowIgnoreCoverage> ignoredGOs = gs.ignoredGOs;
            int ignoredGOsCount = ignoredGOs.Count;


            bool updateCoverage = false;
            if (_coverageUpdateMethod != SNOW_COVERAGE_UPDATE_METHOD.Disabled) {
                if (depthTexture == null || !depthTexture.IsCreated()) {
                    CleanUpTextureDepth();
                    needUpdateSnowCoverage = true;
                }

                updateCoverage = !Application.isPlaying || needUpdateSnowCoverage || _coverageUpdateMethod == SNOW_COVERAGE_UPDATE_METHOD.EveryFrame;
                if (!updateCoverage && _coverageUpdateMethod == SNOW_COVERAGE_UPDATE_METHOD.Discrete) {
                    updateCoverage = (lastCameraEffectPosition - cameraEffect.transform.position).sqrMagnitude > 2500;
                }

#if UNITY_EDITOR
                if (Camera.current.cameraType == CameraType.SceneView) { 
                    updateCoverage = true;
                }
#endif
            }

            bool forward = !deferred && (!_distanceOptimization || _detailDistance > 0);
            if (updateCoverage || forward) {
                for (int k = 0; k < ignoredGOsCount; k++) {
                    GlobalSnowIgnoreCoverage igo = ignoredGOs[k];
                    if (igo != null && igo.isActiveAndEnabled && !igo.blockSnow) {
                        igo.layer = igo.gameObject.layer;
                        igo.gameObject.layer = _defaultExclusionLayer;
                    }
                }
                if (updateCoverage) {
                    lastCameraEffectPosition = cameraEffect.transform.position;
                    UpdateSnowCoverageNow(); // get snow coverage on 50 m. position change
                }
            }

            // Render snow scene
            float brightness;
            if (_sun != null) {
                Shader.SetGlobalVector("_GS_SunDir", new Vector4(_sun.transform.forward.x, _sun.transform.forward.y, _sun.transform.forward.z, _terrainMarksViewDistance * _terrainMarksViewDistance));
                brightness = Mathf.Clamp01(1.5f * _maxExposure / (_maxExposure + Mathf.Max(0.001f, -_sun.transform.forward.y)));
            } else {
                Shader.SetGlobalVector("_GS_SunDir", new Vector4(0, -0.3f, 0.7f, _terrainMarksViewDistance * _terrainMarksViewDistance));
                brightness = 1f;
            }
            Shader.SetGlobalVector("_GS_SnowData1", new Vector4(_reliefAmount, _occlusionIntensity, _glitterStrength, brightness));

            if (deferred) {
                SnowRender_DeferredPath();
                CheckCommandBuffer();
            } else if (forward) {
                SnowRender_ForwardPath();
            }

            // Restore exclusion list
            if (updateCoverage) {
                for (int k = 0; k < ignoredGOsCount; k++) {
                    GlobalSnowIgnoreCoverage igo = ignoredGOs[k];
                    igo.gameObject.layer = igo.layer;
                }
            }

        }

        private void OnPreRender() {
            if (cbMat != null && cameraEffect != null) {
                // remove camera flickering by shifting temporarily the camera to 0 position
                Vector3 camPos = cameraEffect.transform.position;
                cbMat.SetVector("_FlickerFreeCamPos", camPos);
                cameraEffect.transform.position = Vector3.zero;
                cameraEffect.ResetWorldToCameraMatrix();
                cbMat.SetMatrix("_ClipToWorld", cameraEffect.cameraToWorldMatrix); // * cameraEffect.projectionMatrix.inverse);
                cameraEffect.transform.position = camPos;
            }
        }

        void Update() {
            if (cameraEffect != null) {
                if (snowfallSystem != null) {
                    snowfallSystem.transform.position = GlobalSnow.instance.cameraEffect.transform.position + new Vector3(0, snowfallSystemAltitude, 0);
                }

                if (lastCameraAllowHDR != cameraEffect.allowHDR || lastRenderingPath != cameraEffect.actualRenderingPath) {
                    UpdateProperties();
                }
            }

            if (_terrainMarks && _terrainMarksAutoFPS && (transform.position - lastCameraMarkPosition).sqrMagnitude > 0.5f) {
                Vector3 dir = transform.position - lastCameraMarkPosition;
                Vector3 dirxz = new Vector3(dir.z, 0, -dir.x).normalized * UnityEngine.Random.Range(0.11f, 0.35f);
                Vector3 medPos = (lastCameraMarkPosition + transform.position) * 0.5f;
                lastCameraMarkPosition = transform.position;
                MarkSnowAt(medPos + dirxz, 0.185f);
                MarkSnowAt(transform.position - dirxz, 0.185f);
            }

        }

        private void CheckCommandBuffer() {
            if (!needRebuildCommandBuffer) {
                if (excludedRenderers.Length < excludedRenderersCount) {
                    excludedRenderersCount = excludedRenderers.Length;
                }
                bool dirtyVisible = Time.frameCount < 5;
                for (int k = 0; k < excludedRenderersCount; k++) {
                    Renderer r = excludedRenderers[k].renderer;
                    if (r == null) {
                        for (int j = k; j < excludedRenderersCount - 1; j++) {
                            excludedRenderers[j] = excludedRenderers[j + 1];
                        }
                        excludedRenderersCount--;
                        k--;
                    } else {
                        bool isVisible = dirtyVisible || r.isVisible;
                        if (isVisible != excludedRenderers[k].wasVisible) {
                            excludedRenderers[k].wasVisible = isVisible;
                            needRebuildCommandBuffer = true;
                        }
                    }
                }
            }
            if (needRebuildCommandBuffer) {
                RebuildCommandBuffer();
            }
        }

        #endregion

        #region Internal stuff

        void CheckSunOcclusion() {
            if (cameraEffect == null || _sun == null)
                return;
            if (_sun.transform.rotation != lastSunRotation) {
                lastSunRotation = _sun.transform.rotation;
                sunOccluded = _sun.transform.forward.y > 0;
                UpdateSnowData3();
            }
        }

        void UpdateSnowTintColor() {
            Shader.SetGlobalColor("_GS_SnowTint", _snowTint);
        }


        void UpdateSnowData2(float minimumAltitude) {
            if (_coverageExtension > 1 && _coverageResolution == 1)
                _coverageResolution = 2;
            float coverageExtensionValue = 1f / Mathf.Pow(2, 8f + _coverageExtension);
            Shader.SetGlobalVector("_GS_SnowData2", new Vector4(minimumAltitude + _minimumAltitudeVegetationOffset, 10f * _altitudeScatter, coverageExtensionValue, minimumAltitude));
        }

        void UpdateSnowData3() {
            float y = _sun != null ? _sun.transform.forward.y : -0.3f;
            Shader.SetGlobalVector("_GS_SnowData3", new Vector4(sunOccluded ? 0f : 1f, Mathf.Clamp01(y * -100f), _groundCoverage + 0.0012f, (2f * (_grassCoverage * Mathf.Min(_snowAmount, 1f)) - 1f)));
        }

        void UpdateSnowData4() {
            Shader.SetGlobalVector("_GS_SnowData4", new Vector4(1f / _footprintsScale, _footprintsObscurance, 0, 1.0f - (_billboardCoverage * Mathf.Min(_snowAmount, 1f))));
        }

        void UpdateSnowData5() {
            Shader.SetGlobalVector("_GS_SnowData5", new Vector4(_slopeThreshold - 0.2f, 5f + _slopeSharpness * 10f, _slopeNoise * 5f, _distanceSlopeThreshold - 0.2f));
        }

        void UpdateSnowData6() {
            Shader.SetGlobalVector("_GS_SnowData6", new Vector4(1f - Mathf.Min(_snowAmount, 1f), _smoothness, _altitudeBlending, Mathf.Max(_snowAmount - 1f, 0)));
        }

        /// <summary>
        /// In case Sun is not set, locate any directional light on the scene.
        /// </summary>
        void SetupSun() {
            if (_sun == null) {
                Light[] lights = FindObjectsOfType<Light>();
                Light directionalLight = lights.FirstOrDefault(l => l != null && l.type == LightType.Directional);
                if (directionalLight != null) {
                    _sun = directionalLight.gameObject;
                }
            }
        }

        void CleanUpTextureDepth() {
            if (depthTexture != null) {
                depthTexture.Release();
                depthTexture = null;
            }
        }

        void CleanUpTextureSnowScene() {
            if (snowedSceneTexture != null) {
                RenderTexture.ReleaseTemporary(snowedSceneTexture);
                snowedSceneTexture = null;
            }
            if (snowedSceneTexture2 != null) {
                RenderTexture.ReleaseTemporary(snowedSceneTexture2);
                snowedSceneTexture2 = null;
            }
        }

        float GetAngle(Vector2 v1, Vector2 v2) {
            float sign = Mathf.Sign(v1.x * v2.y - v1.y * v2.x);
            return Vector2.Angle(v1, v2) * sign;
        }

        #endregion

        #region Property handling


        void OnDidApplyAnimationProperties() {   // support for animating property based fields
            needsUpdateProperties = true;
        }

        public void UpdateProperties() {
            if (Application.isPlaying) {
                needsUpdateProperties = true;
            } else {
                UpdatePropertiesNow();
            }
        }

        void UpdatePropertiesNow() {

            if (GlobalSnow.instance == null)
                return;

            // Setup materials & shaders
            LoadResources();

            needUpdateSnowCoverage = true;

            UpdateSnowTintColor();
            UpdateSnowData2(_minimumAltitude);
            UpdateSnowData3();
            UpdateSnowData4();
            UpdateSnowData5();
            UpdateSnowData6();

            if (_coverageMask) {
                Shader.SetGlobalTexture("_GS_DepthMask", _coverageMaskTexture);
            } else {
                Shader.SetGlobalTexture("_GS_DepthMask", Texture2D.whiteTexture);
            }
            Shader.SetGlobalVector("_GS_DepthMaskWorldSize", new Vector4(_coverageMaskWorldSize.x, _coverageMaskWorldCenter.x, _coverageMaskWorldSize.z, _coverageMaskWorldCenter.z));

            if (_footprintsTexture == null) {
                _footprintsTexture = Resources.Load<Texture2D>("Common/Textures/Footprint");
            }
            Shader.SetGlobalTexture("_GS_DetailTex", _footprintsTexture);

            if (_snowQuality == SNOW_QUALITY.FlatShading) {
                Shader.DisableKeyword(SKW_RELIEF);
                Shader.DisableKeyword(SKW_OCLUSSION);
                Shader.EnableKeyword(SKW_FLAT_SHADING);
            } else {
                Shader.DisableKeyword(SKW_FLAT_SHADING);
                if (_snowQuality == SNOW_QUALITY.ReliefMapping) {
                    if (_occlusion) {
                        Shader.DisableKeyword(SKW_RELIEF);
                        Shader.EnableKeyword(SKW_OCLUSSION);
                    } else {
                        Shader.DisableKeyword(SKW_OCLUSSION);
                        Shader.EnableKeyword(SKW_RELIEF);
                    }
                } else {
                    Shader.DisableKeyword(SKW_RELIEF);
                    Shader.DisableKeyword(SKW_OCLUSSION);
                }
            }
            if (_footprints) {
                if (characterController == null)
                    characterController = FindObjectOfType<CharacterController>();
                Shader.EnableKeyword(SKW_FOOTPRINTS);
            } else {
                Shader.DisableKeyword(SKW_FOOTPRINTS);
            }
            if (_terrainMarks) {
                Shader.EnableKeyword(SKW_TERRAIN_MARKS);
            } else {
                Shader.DisableKeyword(SKW_TERRAIN_MARKS);
            }
            if (_opaqueCutout) {
                Shader.EnableKeyword(SKW_GLOBALSNOW_OPAQUE_CUTOUT);
            } else {
                Shader.DisableKeyword(SKW_GLOBALSNOW_OPAQUE_CUTOUT);
            }
            if (_updateSpeedTree && _speedTreeRemoveLeaves) {
                Shader.EnableKeyword(SKW_REMOVE_LEAVES);
            } else {
                Shader.DisableKeyword(SKW_REMOVE_LEAVES);
            }

            UpdateSnowfallProperties();

            lastRenderingPath = cameraEffect.actualRenderingPath;

            composeMatKeywords.Clear();

            if (deferred) {
                CheckTerrainsCollisionDetectors();
                composeMatKeywords.Add(SKW_NO_SNOW);
            } else {
                UpdateScene();
                UpdateMaterials();

                if (_distanceOptimization) {
                    cameraEffect.depthTextureMode |= DepthTextureMode.Depth;
                    distanceMatKeywords.Clear();
                    if (_distanceIgnoreNormals) {
                        distanceMatKeywords.Add(SKW_IGNORE_NORMALS);
                    } else {
                        if (cameraEffect.actualRenderingPath == RenderingPath.DeferredShading) {
                            distanceMatKeywords.Add(SKW_GBUFFER_NORMALS);
                        } else {
                            cameraEffect.depthTextureMode |= DepthTextureMode.DepthNormals;
                        }
                    }
                    if (_distanceIgnoreCoverage) {
                        distanceMatKeywords.Add(SKW_IGNORE_COVERAGE);
                    }
                    distantSnowMat.shaderKeywords = distanceMatKeywords.ToArray();
                    distantSnowMat.SetColor("_Color", _distanceSnowColor);
                    distantSnowMat.SetFloat("_Distance01", 0.000001f + (_detailDistance * 0.9f) / cameraEffect.farClipPlane);
                }

                if (_forceSPSR && UnityEngine.XR.XRSettings.enabled && Application.isPlaying) {
                    composeMatKeywords.Add(SKW_FORCE_STEREO_RENDERING);
                }
                if (_distanceOptimization) {
                    if (_detailDistance == 0) {
                        composeMatKeywords.Add(SKW_JUST_DISTANT_SNOW);
                    } else {
                        composeMatKeywords.Add(SKW_USE_DISTANT_SNOW);
                    }
                }
                if (!_cameraFrost) {
                    composeMatKeywords.Add(SKW_NO_FROST);
                }
            }
            composeMat.shaderKeywords = composeMatKeywords.ToArray();

            // Image effect renderer
            GlobalSnowImageEffect fr = GetComponent<GlobalSnowImageEffect>();
            if (deferred) {
                if (cameraFrost && fr == null) {
                    fr = gameObject.AddComponent<GlobalSnowImageEffect>();
                } else if (!_cameraFrost && fr != null) {
                    DestroyImmediate(fr);
                    fr = null;
                }
            } else {
                if (fr == null) {
                    fr = gameObject.AddComponent<GlobalSnowImageEffect>();
                }
            }
            if (fr != null) {
                fr.enabled = true;
            }

            // Check WMAPI integration
#if WORLDAPI_PRESENT
												GlobalSnowWMAPI wmapi = gameObject.GetComponent<GlobalSnowWMAPI> ();
												if (_enableWMAPI && wmapi == null) {
																gameObject.AddComponent<GlobalSnowWMAPI> ();
												} else if (!_enableWMAPI && wmapi != null) {
																DestroyImmediate (wmapi);
												}
#endif

            if (OnUpdateProperties != null)
                OnUpdateProperties();
        }

        public void UpdateSnowfallProperties() {
            if (snowfallSystem == null) {
                GameObject go = GameObject.Find(SNOW_PARTICLE_SYSTEM);
                if (go != null) {
                    snowfallSystem = go.GetComponent<ParticleSystem>();
                    if (snowfallSystem == null)
                        DestroyImmediate(go);
                }
            }

            if (_snowfall) {
                if (snowfallSystem == null) {
                    GameObject go = Instantiate(Resources.Load<GameObject>("Common/Prefabs/SnowParticleSystem")) as GameObject;
                    go.name = SNOW_PARTICLE_SYSTEM;
                    go.hideFlags = HideFlags.DontSave;
                    if (go == null) {
                        Debug.Log("SnowParticleSystem not found.");
                        _snowfall = false;
                    } else {
                        snowfallSystem = go.GetComponent<ParticleSystem>();
                    }
                }
                if (snowfallSystem != null) {
                    float snowfallIntensity = _snowfallIntensity * _snowAmount;

                    var emission = snowfallSystem.emission;
#if WORLDAPI_PRESENT
                    emission.rateOverTime = 1000 * snowfallIntensity * WAPI.WorldManager.Instance.SnowPower;
#else
                    emission.rateOverTime = 1000 * snowfallIntensity * _snowAmount;
#endif
                    ParticleSystem.ShapeModule shape = snowfallSystem.shape;
                    shape.scale = new Vector3(_snowfallDistance, _snowfallDistance, 20f);
                    if (_snowfallDistance != lastSnowfallDistance && lastSnowfallDistance > 0) {
                        lastSnowfallIntensity -= 0.001f;
                    }
                    lastSnowfallDistance = _snowfallDistance;
                    ParticleSystem.MainModule main = snowfallSystem.main;
                    main.simulationSpeed = _snowfallSpeed;
                    snowfallSystem.gameObject.layer = SNOW_PARTICLES_LAYER;
                    ParticleSystemRenderer r = snowfallSystem.GetComponent<ParticleSystemRenderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (_snowfallReceiveShadows) {
                        r.sharedMaterial = snowParticleIllumOpaque;
                        r.receiveShadows = true;
                    } else if (_snowfallUseIllumination) {
                        r.sharedMaterial = snowParticleIllum;
                        r.receiveShadows = false;
                    } else {
                        r.sharedMaterial = snowParticle;
                        r.receiveShadows = false;
                    }
                    if (snowfallIntensity != lastSnowfallIntensity) {
                        if (m_Particles == null || m_Particles.Length < snowfallSystem.main.maxParticles) {
                            m_Particles = new ParticleSystem.Particle[snowfallSystem.main.maxParticles];
                        }
                        if (snowfallSystem.particleCount < 200) {
                            snowfallSystem.Emit(200 - snowfallSystem.particleCount);
                        }
                        int actualCount = snowfallSystem.GetParticles(m_Particles);
                        float t = snowfallIntensity / lastSnowfallIntensity;
                        if (snowfallIntensity < lastSnowfallIntensity) {
                            for (int k = 0; k < actualCount; k++) {
                                if (UnityEngine.Random.value > t) {
                                    m_Particles[k].remainingLifetime = -1;
                                }
                            }
                        } else if (snowfallIntensity > lastSnowfallIntensity) {
                            int maxCount = (int)(actualCount * t);
                            if (maxCount > m_Particles.Length) maxCount = m_Particles.Length;
                            Vector3 camPos = cameraEffect.transform.position;
                            float distance = _snowfallDistance * 0.5f;
                            snowfallSystemAltitude = Mathf.Min(50f, distance);
                            for (int k = actualCount; k < maxCount; k++) {
                                m_Particles[k] = m_Particles[k - actualCount];
                                Vector3 randomPos = UnityEngine.Random.insideUnitSphere;
                                m_Particles[k].position = new Vector3(camPos.x + randomPos.x * distance, camPos.y + randomPos.y * snowfallSystemAltitude, camPos.z + randomPos.z * distance);
                                m_Particles[k].remainingLifetime = 10f + k % 10;
                            }
                            actualCount = maxCount;
                        }
                        snowfallSystem.SetParticles(m_Particles, actualCount);
                        lastSnowfallIntensity = snowfallIntensity;
                    }
                }
            } else if (snowfallSystem != null) {
                DestroyImmediate(snowfallSystem.gameObject);
                snowfallSystem = null;
            }
        }

        void UpdateMaterials() {
            if (!_fixMaterials)
                return;

            const int RENDERQUEUE_TRANSPARENT = 3000;
            Material[] materials = FindObjectsOfType<Material>();
            for (int k = 0; k < materials.Length; k++) {
                Material mat = materials[k];
                if (mat == null || mat.shader == null || mat.renderQueue >= RENDERQUEUE_TRANSPARENT)
                    continue;
                string renderType = mat.GetTag("RenderType", false, "");
                if (renderType.Length == 0) {
                    mat.SetOverrideTag("RenderType", "Opaque");
                }
            }
        }

        void UpdateLODMaterials(LODGroup lodGroup) {
            if (lodGroup == null)
                return;
            Shader originalSpeedTreeBillboardShader = Shader.Find("Nature/SpeedTree Billboard");

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null)
                return;
            for (int ld = 0; ld < lods.Length; ld++) {
                Renderer[] rr = lods[ld].renderers;
                if (rr != null) {
                    for (int r = 0; r < rr.Length; r++) {
                        Renderer renderer = rr[r];
                        if (renderer == null)
                            continue;
                        if (renderer is BillboardRenderer) {
                            BillboardRenderer br = (BillboardRenderer)renderer;
                            if (br.billboard == null)
                                continue;
                            Material mat = br.billboard.material;
                            if (mat != null) {
                                if (_updateSpeedTree && mat.shader != null && mat.shader.name.Equals("Nature/SpeedTree Billboard")) {
                                    mat.shader = snowedSpeedTreeBillboardShader; // has "RenderType" = "SnowedSpeedTreeBillboard";
                                } else if (!_updateSpeedTree && mat.shader == snowedSpeedTreeBillboardShader && originalSpeedTreeBillboardShader != null) {
                                    mat.shader = originalSpeedTreeBillboardShader;
                                }
                            }
                        } else {
                            Material[] materials = renderer.sharedMaterials;
                            if (materials == null)
                                continue;
                            for (int k = 0; k < materials.Length; k++) {
                                Material mat = materials[k];
                                if (mat != null && mat.shader != null) {
                                    if (mat.shader.name.Equals("Nature/SpeedTree")) {
                                        if (_updateSpeedTree) {
                                            if (_speedTreeRemoveLeaves) {
                                                mat.shader = Shader.Find("GlobalSnow/SpeedTreeNoLeaves");
                                            } else {
                                                mat.SetOverrideTag("RenderType", "SnowedSpeedTree");
                                            }
                                        } else if (mat.GetTag("RenderType", false, "").Equals("SnowedSpeedTree")) {
                                            mat.SetOverrideTag("RenderType", "Opaque");
                                        }
                                    } else if (mat.shader.name.Equals("GlobalSnow/SpeedTreeNoLeaves")) {
                                        if (!_updateSpeedTree || !_speedTreeRemoveLeaves) {
                                            mat.shader = Shader.Find("Nature/SpeedTree");
                                        }
                                        if (_updateSpeedTree) {
                                            mat.SetOverrideTag("RenderType", "SnowedSpeedTree");
                                        } else if (mat.GetTag("RenderType", false, "").Equals("SnowedSpeedTree")) {
                                            mat.SetOverrideTag("RenderType", "Opaque");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        void UpdateScene() {
            Terrain[] activeTerrains = Terrain.activeTerrains;
            if (activeTerrains != null) {
                for (int k = 0; k < activeTerrains.Length; k++) {
                    Terrain activeTerrain = activeTerrains[k];
                    UpdateTerrain(activeTerrain);
                }
            }

            // Affect all standalone trees as well
            LODGroup[] lodGroups = FindObjectsOfType<LODGroup>();
            for (int k = 0; k < lodGroups.Length; k++) {
                LODGroup lodGroup = lodGroups[k];
                UpdateLODMaterials(lodGroup);
            }

            // Affect standalone materials
            Material[] materials = FindObjectsOfType<Material>();
            for (int k = 0; k < materials.Length; k++) {
                Material mat = materials[k];
                if (mat != null && mat.shader != null) {
                    if (mat.shader.name.Equals("Nature/SpeedTree")) {
                        if (_updateSpeedTree) {
                            if (_speedTreeRemoveLeaves) {
                                mat.shader = Shader.Find("GlobalSnow/SpeedTreeNoLeaves");
                            } else {
                                mat.SetOverrideTag("RenderType", "SnowedSpeedTree");
                            }
                        } else if (mat.GetTag("RenderType", false, "").Equals("SnowedSpeedTree")) {
                            mat.SetOverrideTag("RenderType", "Opaque");
                        }
                    } else if (mat.shader.name.Equals("GlobalSnow/SpeedTreeNoLeaves")) {
                        if (!_updateSpeedTree || !_speedTreeRemoveLeaves) {
                            mat.shader = Shader.Find("Nature/SpeedTree");
                        }
                        if (_updateSpeedTree) {
                            mat.SetOverrideTag("RenderType", "SnowedSpeedTree");
                        } else if (mat.GetTag("RenderType", false, "").Equals("SnowedSpeedTree")) {
                            mat.SetOverrideTag("RenderType", "Opaque");
                        }
                    }
                }
            }
        }

        void UpdateTerrain(Terrain terrain) {
            if (terrain == null)
                return;
            CheckTerrainCollisionDetector(terrain);

            TreePrototype[] tps = terrain.terrainData.treePrototypes;
            for (int prot = 0; prot < tps.Length; prot++) {
                if (tps[prot] != null && tps[prot].prefab != null) {
                    LODGroup lodGroup = tps[prot].prefab.GetComponent<LODGroup>();
                    UpdateLODMaterials(lodGroup);
                }
            }
        }

        void CheckTerrainsCollisionDetectors() {
            Terrain[] activeTerrains = Terrain.activeTerrains;
            if (activeTerrains != null) {
                for (int k = 0; k < activeTerrains.Length; k++) {
                    Terrain activeTerrain = activeTerrains[k];
                    CheckTerrainCollisionDetector(activeTerrain);
                }
            }
        }

        void CheckTerrainCollisionDetector(Terrain terrain) {
            GlobalSnowCollisionDetector cd = terrain.GetComponent<GlobalSnowCollisionDetector>();
            if (_terrainMarks) {
                if (cd == null)
                    terrain.gameObject.AddComponent<GlobalSnowCollisionDetector>();
            } else {
                DestroyImmediate(cd);
            }
        }

        #endregion

        #region Gizmos

        void OnDrawGizmosSelected() {
            if (_showCoverageGizmo && zenithCam != null) {
                Vector3 pos = zenithCam.transform.position;
                pos.y = -10;
                float viewSize = zenithCam.orthographicSize * 2f;
                Vector3 size = new Vector3(viewSize, 0.1f, viewSize);
                Gizmos.color = Color.blue;
                for (int k = 0; k < 5; k++) {
                    Gizmos.DrawWireCube(pos, size);
                    pos.y += 0.5f;
                    Gizmos.DrawWireCube(pos, size);
                    pos.y += 0.5f;
                }
            }
        }

        #endregion


        #region Misc tools

        /// <summary>
        /// Makes Global Snow ignore a gameobject. Used internally.
        /// </summary>
        public void IgnoreGameObject(GlobalSnowIgnoreCoverage o) {
            if (o == null) return;
            if (!ignoredGOs.Contains(o)) {
                ignoredGOs.Add(o);
                needUpdateSnowCoverage = true;
                if (deferred) {
                    needRebuildCommandBuffer = true;
                }
            }
        }

        /// <summary>
        /// Makes Global Snow use a gameobject for snow coverage. Used internally.
        /// </summary>
        /// <param name="o">O.</param>
        public void UseGameObject(GlobalSnowIgnoreCoverage o) {
            if (o == null) return;
            if (ignoredGOs.Contains(o)) {
                ignoredGOs.Remove(o);
                if (deferred) {
                    needRebuildCommandBuffer = true;
                }

            }
        }

        /// <summary>
        /// Leaves a mark on the snow at a given world space position.
        /// </summary>
        public void MarkSnowAt(Vector3 position) {
            MarkSnowAt(position, _terrainMarksDefaultSize);
        }

        /// <summary>
        /// Leaves a mark on the snow at a given world space position and radius.
        /// </summary>
        public void MarkSnowAt(Vector3 position, float radius) {
            if (radius <= 0)
                radius = _terrainMarksDefaultSize;
            decalRequests.Add(new Vector4(position.x, position.y, position.z, radius));
        }

        /// <summary>
        /// Refresh snow coverage
        /// </summary>
        public void UpdateSnowCoverage() {
            needUpdateSnowCoverage = true;
        }

        /// <summary>
        /// Forces command buffer to be rebuilt
        /// </summary>
        /// <param name="performFullSceneScan">If true, the full scene will be parsed to find excluded objects also according to the layer mask setting. This can be time consuming so it usually is only done once at the start of the scene.</param>
        public void RefreshExcludedObjects(bool performFullSceneScan = false) {
            if (performFullSceneScan) {
                this.performFullSceneScan = true;
            }
            needRebuildCommandBuffer = true;
        }

        Dictionary<GameObject, SnowColliderInfo> collisionCache = new Dictionary<GameObject, SnowColliderInfo>();

        /// <summary>
        /// Rports a continous collision by an object with ground. This method takes care of spreading terrain marks depending on distance travelled and mark size.
        /// </summary>
        /// <param name="collision">Collision data in case there's rigidbody involved</param>
        /// <param name="contactPoint">Direct collision point in world space if no rigidbody is involved</param>
        public void CollisionStay(GameObject go, Collision collision, Vector3 contactPoint) {
            if (go == null) return;
            // Ensure this object is visible for the camera
            if (snowCamera != null && (snowCamera.cullingMask & (1 << go.layer)) == 0)
                return;

            // Get snow mark size
            float collisionDistanceThreshold = 0.1f;
            float maxStep = terrainMarksStepMaxDistance;
            float rotationThreshold = terrainMarksRotationThreshold;
            SnowColliderInfo newColliderInfo;
            GlobalSnowColliderExtraInfo ci = go.GetComponentInParent<GlobalSnowColliderExtraInfo>();
            if (ci != null) {
                newColliderInfo.markSize = ci.markSize;
                collisionDistanceThreshold = ci.collisionDistanceThreshold;
                rotationThreshold = ci.rotationThreshold;
                maxStep = ci.stepMaxDistance;
            } else {
                newColliderInfo.markSize = terrainMarksDefaultSize;
            }

            // Check gameobject position change
            Vector3 currentPos = go.transform.position;
            Vector3 currentForward = go.transform.forward;
            SnowColliderInfo colliderInfo;
            Vector3 moveDir = Vector3.zero;
            float steps = 1f;
            if (collisionCache.TryGetValue(go, out colliderInfo)) {
                Vector3 oldPosition = colliderInfo.position;
                moveDir = currentPos - oldPosition;
                float diffSqr = moveDir.sqrMagnitude;
                float angleDiff = Vector3.Angle(colliderInfo.forward, currentForward);
                if (diffSqr < collisionDistanceThreshold && angleDiff < rotationThreshold)
                    return;

                maxStep *= maxStep;
                if (diffSqr < maxStep) {
                    float diff = Mathf.Sqrt(diffSqr);
                    steps = diff / newColliderInfo.markSize;
                    if (steps > 100) steps = 100;
                    steps = ((int)steps) + 1;
                }
            }
            newColliderInfo.position = currentPos;
            newColliderInfo.forward = currentForward;
            collisionCache[go] = newColliderInfo;
            if (collision != null) {
#if UNITY_2018_3_OR_NEWER
                for (int k = 0; k < collision.contactCount && k < 5; k++) {
                    ContactPoint cp = collision.GetContact(k);
                    SendSnowMarkAt(cp.point, moveDir, steps, newColliderInfo.markSize);
                }
#else
                ContactPoint[] cps = collision.contacts;
                for (int k = 0; k < cps.Length && k < 5; k++) {
                    SendSnowMarkAt(cps[k].point, moveDir, steps, newColliderInfo.markSize);
                }
#endif
            } else {
                SendSnowMarkAt(contactPoint, moveDir, steps, newColliderInfo.markSize);
            }
        }

        void SendSnowMarkAt(Vector3 currMarkPos, Vector3 moveDir, float steps, float markSize) {
            if (steps < 2) {
                MarkSnowAt(currMarkPos, markSize);
            } else {
                Vector3 prevMarkpos = currMarkPos - moveDir;
                for (int s = 1; s <= steps; s++) {
                    Vector3 snowMarkPos = Vector3.Lerp(currMarkPos, prevMarkpos, s / steps);
                    MarkSnowAt(snowMarkPos, markSize);
                }
            }
        }


        public void FootprintAt(Vector3 position, Vector3 moveDir) {
            requestedFootprint = true;
            requestedFootprintPos = position;
            requestedFootprintDir = moveDir;
        }


        public void CollisionStop(GameObject go) {
            SnowColliderInfo colliderInfo;
            if (collisionCache.TryGetValue(go, out colliderInfo)) {
                Vector3 diff = colliderInfo.position - go.transform.position;
                if (diff.sqrMagnitude >= 0.01f) {
                    collisionCache.Remove(go);
                }
            }
        }

#endregion

    }

}