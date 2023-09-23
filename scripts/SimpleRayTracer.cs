using System;
using System.Diagnostics;
using Godot;
using Godot.Collections;

namespace BasicRaytracer.scripts;

public partial class SimpleRayTracer : Node
{

    [Export] public RDShaderFile ShaderFile;
    
    private Vector2I _imageSize;
    private float _globalTime;
    private readonly RenderingDevice _rd = RenderingServer.CreateLocalRenderingDevice();
    private Rid _uniformSet;
    private Rid _pipeline;
    private Array<RDUniform> _bindings;
    private Rid _shader;
    private Rid _outputTexture;

    private Camera3D _camera;
    private ComputeOutput _textureRect;
    private DirectionalLight3D _directionalLight;
    
    public override void _Ready()
    {
        base._Ready();

        _camera = GetNode<Camera3D>("Camera");
        _textureRect = GetNode<ComputeOutput>("Camera/RayTracerSimple/ComputeOutput");
        _directionalLight = GetNode<DirectionalLight3D>("DirectionalLight");
        
        Debug.Assert(_camera != null);
        Debug.Assert(_textureRect != null);
        Debug.Assert(_directionalLight != null);
        
        _imageSize.X = (int) ProjectSettings.GetSetting("display/window/size/viewport_width");
        _imageSize.Y = (int) ProjectSettings.GetSetting("display/window/size/viewport_height");
        
        _textureRect.TextureInit(_imageSize);
        
        SetupCompute();
        Render();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateCompute();
        Render(delta);
    }
    
    // ReSharper disable once MemberCanBePrivate.Global
    public void SetupCompute()
    {
        // Create shader and pipeline
        var shaderSpirV = ShaderFile.GetSpirV();
        _shader = _rd.ShaderCreateFromSpirV(shaderSpirV);
        _pipeline = _rd.ComputePipelineCreate(_shader);
        
        // Data for compute shaders has to come as an array of bytes
        // The rest of this function is just creating storage buffers and texture uniforms
        
        // Camera Matrices Buffer
        var camToWorld = _MatrixToBytes(_camera.GlobalTransform);
        var camData2 = new [] {70.0f, 4000.0f, 0.05f};
        var camData2Byte = new byte[camData2.Length * sizeof(float)];
        Buffer.BlockCopy(camData2, 0, camData2Byte, 0, camData2Byte.Length);
        var cameraMatricesBytes = new byte[camToWorld.Length + camData2Byte.Length];
        camToWorld.CopyTo(cameraMatricesBytes, 0);
        camData2Byte.CopyTo(cameraMatricesBytes, camToWorld.Length);
        var cameraMatricesBuffer = _rd.StorageBufferCreate((uint)cameraMatricesBytes.Length, cameraMatricesBytes);
        var cameraMatricesUniform = new RDUniform();
        cameraMatricesUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        cameraMatricesUniform.Binding = 0;
        cameraMatricesUniform.AddId(cameraMatricesBuffer);
        
        // Directional Light Buffer
        var lightDirection = -_directionalLight.GlobalTransform.Basis.Z;
        lightDirection = lightDirection.Normalized();
        var lightData = new []
        {
            lightDirection.X, lightDirection.Y, lightDirection.Z,
            _directionalLight.LightEnergy
        };
        var lightDataBytes = new byte[lightData.Length * sizeof(float)];
        Buffer.BlockCopy(lightData, 0, lightDataBytes, 0, lightDataBytes.Length);
        var lightDataBuffer = _rd.StorageBufferCreate((uint)lightDataBytes.Length, lightDataBytes);
        var lightDataUniform = new RDUniform();
        lightDataUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        lightDataUniform.Binding = 1;
        lightDataUniform.AddId(lightDataBuffer);
        
        // Output Texture Buffer
        var fmt = new RDTextureFormat();
        fmt.Width = (uint) _imageSize.X;
        fmt.Height = (uint) _imageSize.Y;
        fmt.Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;
        fmt.UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.StorageBit |
                        RenderingDevice.TextureUsageBits.CanCopyFromBit;
        var view = new RDTextureView();
        var outputImage = Image.Create(_imageSize.X, _imageSize.Y, false, Image.Format.Rgbaf);
        _outputTexture = _rd.TextureCreate(fmt, view, new Array<byte[]>() {outputImage.GetData()});
        var outputTextureUniform = new RDUniform();
        outputTextureUniform.UniformType = RenderingDevice.UniformType.Image;
        outputTextureUniform.Binding = 2;
        outputTextureUniform.AddId(_outputTexture);
        
        // Global Parameters
        var parameters = new []
        {
            _globalTime,
        };
        var parametersBytes = new byte[parameters.Length * sizeof(float)];
        Buffer.BlockCopy(parameters, 0, parametersBytes, 0, parametersBytes.Length);
        var paramsBuffer = _rd.StorageBufferCreate((uint)parametersBytes.Length, parametersBytes);
        var paramsUniform = new RDUniform();
        paramsUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        paramsUniform.Binding = 3;
        paramsUniform.AddId(paramsBuffer);

        // Create uniform set using the storage buffers
        // The order of the uniforms in the array doesn't matter
        // This is because the RDUniform.binding property already defines its index in the uniform set
        
        _bindings = new Array<RDUniform>()
        {
            cameraMatricesUniform,
            lightDataUniform,
            outputTextureUniform,
            paramsUniform,
        };
        _uniformSet = _rd.UniformSetCreate(_bindings, _shader, 0);

    }

    // ReSharper disable once MemberCanBePrivate.Global
    // This function updates the uniforms with whatever data is changed per-frame
    public void UpdateCompute()
    {
        // update global parameters
        var parameters = new []
        {
            _globalTime,
        };
        var parametersBytes = new byte[parameters.Length * sizeof(float)];
        Buffer.BlockCopy(parameters, 0, parametersBytes, 0, parametersBytes.Length);
        var paramsBuffer = _rd.StorageBufferCreate((uint)parametersBytes.Length, parametersBytes);
        var paramsUniform = new RDUniform();
        paramsUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        paramsUniform.Binding = 3;
        paramsUniform.AddId(paramsBuffer);
        
        // Camera Matrices Buffer
        var camToWorld = _MatrixToBytes(_camera.GlobalTransform);
        var camData2 = new [] {70.0f, 4000.0f, 0.05f};
        var camData2Byte = new byte[camData2.Length * sizeof(float)];
        Buffer.BlockCopy(camData2, 0, camData2Byte, 0, camData2Byte.Length);
        var cameraMatricesBytes = new byte[camToWorld.Length + camData2Byte.Length];
        camToWorld.CopyTo(cameraMatricesBytes, 0);
        camData2Byte.CopyTo(cameraMatricesBytes, camToWorld.Length);
        var cameraMatricesBuffer = _rd.StorageBufferCreate((uint)cameraMatricesBytes.Length, cameraMatricesBytes);
        var cameraMatricesUniform = new RDUniform();
        cameraMatricesUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        cameraMatricesUniform.Binding = 0;
        cameraMatricesUniform.AddId(cameraMatricesBuffer);

        _bindings[3] = paramsUniform;
        _bindings[0] = cameraMatricesUniform;
        _uniformSet = _rd.UniformSetCreate(_bindings, _shader, 0);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void Render(double delta = 0.0)
    {
        _globalTime += (float) delta;
        
        // Start compute list to start recording our compute commands
        var computeList = _rd.ComputeListBegin();
        // Bind the pipeline, this tells the GPU what shader to use
        _rd.ComputeListBindComputePipeline(computeList, _pipeline);
        // Binds the uniform set with the data we want to give our shader
        _rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        // Dispatch (X,Y,Z) work groups
        _rd.ComputeListDispatch(computeList, (uint) _imageSize.X / 8, (uint) _imageSize.Y / 8, 1);
        
        // Tell the GPU we are done with this compute task
        _rd.ComputeListEnd();
        // Force the GPU to start our commands
        _rd.Submit();
        // Force the CPU to wait for the GPU to finish with the recorded commands
        _rd.Sync();
        
        // Now we can grab our data from the output texture
        var byteData = _rd.TextureGetData(_outputTexture, 0);
        _textureRect.SetData(byteData);
    }

    private byte[] _MatrixToBytes(Transform3D t)
    {
        var basis = t.Basis;
        var origin = t.Origin;
        var floats = new [] {
            basis.X.X, basis.X.Y, basis.X.Z, 1.0f,
            basis.Y.X, basis.Y.Y, basis.Y.Z, 1.0f,
            basis.Z.X, basis.Z.Y, basis.Z.Z, 1.0f,
            origin.X, origin.Y, origin.Z, 1.0f,
        };
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }
    
}