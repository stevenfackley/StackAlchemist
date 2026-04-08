# StackAlchemist: Generation State Machine

This diagram defines the strict states a generation job can exist in, ensuring UI loading bars and background workers stay perfectly in sync.

```mermaid
stateDiagram-v2
    [*] --> Pending : Job Enqueued (Webhook or Direct)
    
    Pending --> Generating : EnginePickedUp
    
    state Generating {
        [*] --> LoadTemplatesByProjectType
        LoadTemplatesByProjectType --> RenderHandlebars
        RenderHandlebars --> CallClaude35Sonnet
        CallClaude35Sonnet --> ParseDelimitedBlocks
        ParseDelimitedBlocks --> ReconstructFiles
        ReconstructFiles --> [*]
    }
    
    Generating --> Building : CodeReconstructed
    
    state Building {
        [*] --> SelectBuildStrategy
        
        SelectBuildStrategy --> DotNetBuildStrategy : ProjectType = DotNetNextJs
        SelectBuildStrategy --> PythonReactBuildStrategy : ProjectType = PythonReact
        
        state DotNetBuildStrategy {
            [*] --> RunDotnetBuild
            RunDotnetBuild --> ExtractCSErrors
        }
        
        state PythonReactBuildStrategy {
            [*] --> RunPipFlake8Pytest
            RunPipFlake8Pytest --> RunNpmEslintTsc
            RunNpmEslintTsc --> ExtractPyTsErrors
        }
        
        DotNetBuildStrategy --> CheckExitCode
        PythonReactBuildStrategy --> CheckExitCode
        
        CheckExitCode --> [*] : Exit Code 0 (Success)
        CheckExitCode --> RequestLLMPatch : Exit Code 1 (Error)
        
        RequestLLMPatch --> ApplyPatch
        ApplyPatch --> SelectBuildStrategy : Retry (retryCount < 3)
        ApplyPatch --> Failed : retryCount >= 3
    }
    
    Building --> Packing : BuildPassed
    Building --> Failed : BuildFailed (Max Retries Exceeded)
    
    Packing --> Uploading : ZipCreated
    Uploading --> Success : UploadedToR2
    
    Success --> [*]
    Failed --> [*]
```

### Implementation Reference

The state machine is implemented in `src/StackAlchemist.Engine/Services/GenerationStateMachine.cs` as a static transition table with special handling for the `BuildFailed` event (retry vs. terminal failure based on `GenerationContext.RetryCount`).

**States** (`GenerationState` enum): `Pending`, `Generating`, `Building`, `Packing`, `Uploading`, `Success`, `Failed`

**Events** (`GenerationEvent` enum): `EnginePickedUp`, `CodeReconstructed`, `BuildPassed`, `BuildFailed`, `ZipCreated`, `UploadedToR2`

**Build Strategy Selection**: The `CompileService` selects between `DotNetBuildStrategy` and `PythonReactBuildStrategy` based on the `ProjectType` field in `GenerationContext`. Both strategies plug into the same retry loop in `CompileWorkerService`.
