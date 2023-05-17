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
//**UniTaskLoopRunnerYieldInitialization * *
//**UniTaskLoopRunnerInitialization * *
//PlayerUpdateTime
//DirectorSampleTime
//AsyncUploadTimeSlicedUpdate  
//SynchronizeInputs  
//SynchronizeState  
//XREarlyUpdate  
//**UniTaskLoopRunnerLastYieldInitialization**  
//**UniTaskLoopRunnerLastInitialization**  
  
//EarlyUpdate  
//---  
//**UniTaskLoopRunnerYieldEarlyUpdate**  
//**UniTaskLoopRunnerEarlyUpdate**  
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
//**UniTaskLoopRunnerLastYieldEarlyUpdate**  
//**UniTaskLoopRunnerLastEarlyUpdate**  
  
//FixedUpdate  
//---  
//**UniTaskLoopRunnerYieldFixedUpdate**  
//**UniTaskLoopRunnerFixedUpdate**  
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
//**UniTaskLoopRunnerLastYieldFixedUpdate**  
//**UniTaskLoopRunnerLastFixedUpdate**  
  
//PreUpdate  
//---  
//**UniTaskLoopRunnerYieldPreUpdate**  
//**UniTaskLoopRunnerPreUpdate**  
//PhysicsUpdate  
//Physics2DUpdate  
//CheckTexFieldInput  
//IMGUISendQueuedEvents  
//NewInputUpdate  
//SendMouseEvents  
//AIUpdate  
//WindUpdate  
//UpdateVideo  
//**UniTaskLoopRunnerLastYieldPreUpdate**  
//**UniTaskLoopRunnerLastPreUpdate**  
  
//Update  
//---  
//**UniTaskLoopRunnerYieldUpdate**  
//**UniTaskLoopRunnerUpdate**  
//*ScriptRunBehaviourUpdate*  
//*ScriptRunDelayedDynamicFrameRate*  
//*ScriptRunDelayedTasks*  
//DirectorUpdate  
//**UniTaskLoopRunnerLastYieldUpdate**  
//**UniTaskLoopRunnerLastUpdate**  
  
//PreLateUpdate  
//---  
//**UniTaskLoopRunnerYieldPreLateUpdate**  
//**UniTaskLoopRunnerPreLateUpdate**  
//AIUpdatePostScript  
//DirectorUpdateAnimationBegin  
//LegacyAnimationUpdate  
//DirectorUpdateAnimationEnd  
//DirectorDeferredEvaluate  
//EndGraphicsJobsAfterScriptUpdate  
//ParticleSystemBeginUpdateAll  
//ConstraintManagerUpdate  
//*ScriptRunBehaviourLateUpdate*  
//**UniTaskLoopRunnerLastYieldPreLateUpdate**  
//**UniTaskLoopRunnerLastPreLateUpdate**  
  
//PostLateUpdate  
//---  
//**UniTaskLoopRunnerYieldPostLateUpdate**  
//**UniTaskLoopRunnerPostLateUpdate**  
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
//**UniTaskLoopRunnerLastYieldPostLateUpdate**  
//**UniTaskLoopRunnerLastPostLateUpdate**  