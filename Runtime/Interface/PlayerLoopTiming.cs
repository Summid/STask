namespace SFramework.Threading.Tasks
{
    /// <summary>
    /// STask新增的自定义PlayerLoop
    /// </summary>
    public enum PlayerLoopTiming
    {
        Initialization = 0,
        LastInitialization = 1,

        EarlyUpdate = 2,
        LastEarlyUpdate = 3,

        FixedUpdate = 4,
        LastFixedUpdate = 5,

        PreUpdate = 6,
        LastPreUpdate = 7,

        Update = 8,
        LastUpdate = 9,

        PreLateUpdate = 10,
        LastPreLateUpdate = 11,

        PostLateUpdate = 12,
        LastPostLateUpdate = 13,

        //需要Unity2020.2及以上版本支持 https://docs.unity3d.com/2020.2/Documentation/ScriptReference/PlayerLoop.TimeUpdate.html
        TimeUpdate = 14,
        LastTimeUpdate = 15,
    }
}

//Initialization
//-- -
//**STaskLoopRunnerYieldInitialization * *
//**STaskLoopRunnerInitialization * *
//PlayerUpdateTime
//DirectorSampleTime
//AsyncUploadTimeSlicedUpdate  
//SynchronizeInputs  
//SynchronizeState  
//XREarlyUpdate  
//**STaskLoopRunnerLastYieldInitialization**  
//**STaskLoopRunnerLastInitialization**  

//EarlyUpdate  
//---  
//**STaskLoopRunnerYieldEarlyUpdate**  
//**STaskLoopRunnerEarlyUpdate**  
//PollPlayerConnection  
//ProfilerStartFrame  
//GpuTimestamp  
//AnalyticsCoreStatsUpdate  
//UnityWebRequestUpdate  
//ExecuteMainThreadJobs  
//ProcessMouseInWindow  
//ClearIntermediateRenderers  
//ClearLines  
//PresentBeforeUpdate  
//ResetFrameStatsAfterPresent  
//UpdateAsyncReadbackManager  
//UpdateStreamingManager  
//UpdateTextureStreamingManager  
//UpdatePreloading  
//RendererNotifyInvisible  
//PlayerCleanupCachedData  
//UpdateMainGameViewRect  
//UpdateCanvasRectTransform  
//XRUpdate  
//UpdateInputManager  
//ProcessRemoteInput  
//*ScriptRunDelayedStartupFrame*  
//UpdateKinect  
//DeliverIosPlatformEvents  
//TangoUpdate  
//DispatchEventQueueEvents  
//PhysicsResetInterpolatedTransformPosition  
//SpriteAtlasManagerUpdate  
//PerformanceAnalyticsUpdate  
//**STaskLoopRunnerLastYieldEarlyUpdate**  
//**STaskLoopRunnerLastEarlyUpdate**  

//FixedUpdate  
//---  
//**STaskLoopRunnerYieldFixedUpdate**  
//**STaskLoopRunnerFixedUpdate**  
//ClearLines  
//NewInputFixedUpdate  
//DirectorFixedSampleTime  
//AudioFixedUpdate  
//*ScriptRunBehaviourFixedUpdate*  
//DirectorFixedUpdate  
//LegacyFixedAnimationUpdate  
//XRFixedUpdate  
//PhysicsFixedUpdate  
//Physics2DFixedUpdate  
//DirectorFixedUpdatePostPhysics  
//*ScriptRunDelayedFixedFrameRate*  
//**STaskLoopRunnerLastYieldFixedUpdate**  
//**STaskLoopRunnerLastFixedUpdate**  

//PreUpdate  
//---  
//**STaskLoopRunnerYieldPreUpdate**  
//**STaskLoopRunnerPreUpdate**  
//PhysicsUpdate  
//Physics2DUpdate  
//CheckTexFieldInput  
//IMGUISendQueuedEvents  
//NewInputUpdate  
//SendMouseEvents  
//AIUpdate  
//WindUpdate  
//UpdateVideo  
//**STaskLoopRunnerLastYieldPreUpdate**  
//**STaskLoopRunnerLastPreUpdate**  

//Update  
//---  
//**STaskLoopRunnerYieldUpdate**  
//**STaskLoopRunnerUpdate**  
//*ScriptRunBehaviourUpdate*  
//*ScriptRunDelayedDynamicFrameRate*  
//*ScriptRunDelayedTasks*  
//DirectorUpdate  
//**STaskLoopRunnerLastYieldUpdate**  
//**STaskLoopRunnerLastUpdate**  

//PreLateUpdate  
//---  
//**STaskLoopRunnerYieldPreLateUpdate**  
//**STaskLoopRunnerPreLateUpdate**  
//AIUpdatePostScript  
//DirectorUpdateAnimationBegin  
//LegacyAnimationUpdate  
//DirectorUpdateAnimationEnd  
//DirectorDeferredEvaluate  
//EndGraphicsJobsAfterScriptUpdate  
//ParticleSystemBeginUpdateAll  
//ConstraintManagerUpdate  
//*ScriptRunBehaviourLateUpdate*  
//**STaskLoopRunnerLastYieldPreLateUpdate**  
//**STaskLoopRunnerLastPreLateUpdate**  

//PostLateUpdate  
//---  
//**STaskLoopRunnerYieldPostLateUpdate**  
//**STaskLoopRunnerPostLateUpdate**  
//PlayerSendFrameStarted  
//DirectorLateUpdate  
//*ScriptRunDelayedDynamicFrameRate*  
//PhysicsSkinnedClothBeginUpdate  
//UpdateRectTransform  
//UpdateCanvasRectTransform  
//PlayerUpdateCanvases  
//UpdateAudio  
//VFXUpdate  
//ParticleSystemEndUpdateAll  
//EndGraphicsJobsAfterScriptLateUpdate  
//UpdateCustomRenderTextures  
//UpdateAllRenderers  
//EnlightenRuntimeUpdate  
//UpdateAllSkinnedMeshes  
//ProcessWebSendMessages  
//SortingGroupsUpdate  
//UpdateVideoTextures  
//UpdateVideo  
//DirectorRenderImage  
//PlayerEmitCanvasGeometry  
//PhysicsSkinnedClothFinishUpdate  
//FinishFrameRendering  
//BatchModeUpdate  
//PlayerSendFrameComplete  
//UpdateCaptureScreenshot  
//PresentAfterDraw  
//ClearImmediateRenderers  
//PlayerSendFramePostPresent  
//UpdateResolution  
//InputEndFrame  
//TriggerEndOfFrameCallbacks  
//GUIClearEvents  
//ShaderHandleErrors  
//ResetInputAxis  
//ThreadedLoadingDebug  
//ProfilerSynchronizeStats  
//MemoryFrameMaintenance  
//ExecuteGameCenterCallbacks  
//ProfilerEndFrame  
//**STaskLoopRunnerLastYieldPostLateUpdate**  
//**STaskLoopRunnerLastPostLateUpdate**  