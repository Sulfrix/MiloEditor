using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;
using System.Numerics;
using ImGuiNET;
using static SDL3.SDL;

namespace ImGuiSDL;

/// <summary>
/// Renders ImGui using SDL GPU
/// </summary>
public unsafe class ImGuiRenderer : IDisposable
{
	/// <summary>
	/// Custom ImGui User-Callback
	/// </summary>
	public delegate void UserCallback(ImDrawListPtr parentList, ImDrawCmdPtr cmd); 

	/// <summary>
	/// SDL GPU Device
	/// </summary>
	public readonly nint Device;

	/// <summary>
	/// SDL Window
	/// </summary>
	public readonly nint Window;

	/// <summary>
	/// ImGui Context
	/// </summary>
	public readonly nint Context;

	/// <summary>
	/// Scales all of the ImGui Content by this amount
	/// </summary>
	public float Scale = 1.0f;

	private readonly nint vertexShader;
	private readonly nint fragmentShader;
	private readonly GpuBuffer vertexBuffer;
	private readonly GpuBuffer indexBuffer;
	private readonly nint pipeline;
	private readonly nint fontTexture;
	private readonly nint sampler;

	private ImDrawVert[] vertices = [];
	private ushort[] indices = [];
	private readonly List<UserCallback> callbacks = [];

	public ImGuiRenderer(nint sdlGpuDevice, nint sdlWindow, nint imGuiContext)
	{
		var io = ImGui.GetIO();

		Device = sdlGpuDevice;
		Window = sdlWindow;
		Context = imGuiContext;

		// default imgui display size & scale
		{
			var display = SDL_GetDisplayForWindow(Window);
			if (display != nint.Zero)
				Scale = SDL_GetDisplayContentScale(display);
			SDL_GetWindowSizeInPixels(Window, out int width, out int height);
			io.DisplaySize = new Vector2(width, height);
		}

		// get shader language
		var driver = SDL_GetGPUDeviceDriver(Device);
		var shaderFormat = driver switch
		{
			"private" => SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_PRIVATE,
			"vulkan" => SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
			"direct3d12" => SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL,
			"metal" => SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL,
			_ => throw new NotImplementedException($"Unknown Shader Format for Driver '{driver}'")
		};
		var shaderExt = shaderFormat switch
		{
			SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_PRIVATE => "spv",
			SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV => "spv",
			SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL => "dxil",
			SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL => "msl",
			_ => throw new NotImplementedException($"Unimplemented Shader Format '{shaderFormat}'")
		};

		// create Vertex and Fragment shaders
		// Shaders are stored as Embedded files (see ImGuiSDL.csproj)
		{
			var vertexCode = GetEmbeddedBytes($"ImGui.vertex.{shaderExt}");
			var vertexEntry = Encoding.UTF8.GetBytes("vertex_main");

			var fragmentCode = GetEmbeddedBytes($"ImGui.fragment.{shaderExt}");
			var fragmentEntry = Encoding.UTF8.GetBytes("fragment_main");

			fixed (byte* vertexCodePtr = vertexCode)
			fixed (byte* vertexEntryPtr = vertexEntry)
			{
				vertexShader = SDL_CreateGPUShader(Device, new()
				{
					code_size = (uint)vertexCode.Length,
					code = vertexCodePtr,
					entrypoint = vertexEntryPtr,
					format = shaderFormat,
					stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX,
					num_samplers = 0,
					num_storage_textures = 0,
					num_storage_buffers = 0,
					num_uniform_buffers = 1
				});

				if (vertexShader == nint.Zero)
					throw new Exception($"{nameof(SDL_CreateGPUShader)} Failed: {SDL_GetError()}");
			}

			fixed (byte* fragmentCodePtr = fragmentCode)
			fixed (byte* fragmentEntryPtr = fragmentEntry)
			{
				fragmentShader = SDL_CreateGPUShader(Device, new()
				{
					code_size = (uint)fragmentCode.Length,
					code = fragmentCodePtr,
					entrypoint = fragmentEntryPtr,
					format = shaderFormat,
					stage = SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT,
					num_samplers = 1,
					num_storage_textures = 0,
					num_storage_buffers = 0,
					num_uniform_buffers = 0
				});

				if (fragmentShader == nint.Zero)
					throw new Exception($"{nameof(SDL_CreateGPUShader)} Failed: {SDL_GetError()}");
			}
		}

		// create graphics pipeline
		{
			var colorTargetDesc = stackalloc SDL_GPUColorTargetDescription[1] {
				new() {
					format = SDL_GetGPUSwapchainTextureFormat(Device, Window),
					blend_state = new()
					{
						src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA,
						dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
						color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
						src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
						dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
						alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
						enable_blend = true,
						enable_color_write_mask = false,
					}
				}
			};

			var vertexBuffDesc = stackalloc SDL_GPUVertexBufferDescription[1] {
				new() {
					slot = 0,
					pitch = (uint)Marshal.SizeOf<ImDrawVert>(),
					input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
					instance_step_rate = 0
				}
			};

			var vertexAttr = stackalloc SDL_GPUVertexAttribute[3] {
				// Position : float2
				new() { 
					format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 
					location = 0, 
					offset = 0
				},
				// TexCoord : float2
				new() { 
					format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 
					location = 1, 
					offset = sizeof(float) * 2
				},
				// Color: uint (ubyte4)
				new() { 
					format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UBYTE4_NORM, 
					location = 2, 
					offset = sizeof(float) * 4
				}
			};

			pipeline = SDL_CreateGPUGraphicsPipeline(Device, new()
			{
				vertex_shader = vertexShader,
				fragment_shader = fragmentShader,
				vertex_input_state = new()
				{
					vertex_buffer_descriptions = vertexBuffDesc,
					num_vertex_buffers = 1,
					vertex_attributes = vertexAttr,
					num_vertex_attributes = 3,
				},
				primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,
				rasterizer_state = new()
				{
					cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
				}, 
				multisample_state = new(),
				depth_stencil_state = new(),
				target_info = new()
				{
					num_color_targets = 1,
					color_target_descriptions = colorTargetDesc
				}
			});
		}

		// create buffers
		{
			vertexBuffer = new(Device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX);
			indexBuffer = new(Device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX);
		}

		// create sampler
		{
			sampler = SDL_CreateGPUSampler(Device, new() {
				min_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
				mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_NEAREST,
				mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
				address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
				address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
				address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
			});
		}

		// setup imgui font
		{
			io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);
			var size = width * height * 4;

			// create texture
			fontTexture = SDL_CreateGPUTexture(Device, new()
			{
				type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
				format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM,
				usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
				width = (uint)width,
				height = (uint)height,
				layer_count_or_depth = 1,
				num_levels = 1,
				sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
			});
			if (fontTexture == nint.Zero)
				throw new Exception($"{nameof(SDL_CreateGPUTexture)} Failed: {SDL_GetError()}");

			// upload texture data
			var transferBuffer = SDL_CreateGPUTransferBuffer(Device, new() {
				usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
				size = (uint)size
			});

			var transferPtr = SDL_MapGPUTransferBuffer(Device, transferBuffer, false);
			Buffer.MemoryCopy(pixels, (void*)transferPtr, size, size);
			SDL_UnmapGPUTransferBuffer(Device, transferBuffer);

			var cmd = SDL_AcquireGPUCommandBuffer(Device);
			var pass = SDL_BeginGPUCopyPass(cmd);

			SDL_UploadToGPUTexture(pass,
				source: new() {
					transfer_buffer = transferBuffer,
					offset = 0,
				},
				destination: new() {
					texture = fontTexture,
					w = (uint)width,
					h = (uint)height,
					d = 1
				},
				cycle: false
			);
			SDL_EndGPUCopyPass(pass);
			SDL_SubmitGPUCommandBuffer(cmd);
			SDL_ReleaseGPUTransferBuffer(Device, transferBuffer);
		}

		// set imgui font texture id
		io.Fonts.SetTexID(fontTexture);
	}

	~ImGuiRenderer()
		=> Dispose();

	/// <summary>
	/// Destroys the GPU resources used by the Renderer
	/// </summary>
	public void Dispose()
	{
		GC.SuppressFinalize(this);

		SDL_ReleaseGPUShader(Device, vertexShader);
		SDL_ReleaseGPUShader(Device, fragmentShader);
		SDL_ReleaseGPUGraphicsPipeline(Device, pipeline);
		SDL_ReleaseGPUTexture(Device, fontTexture);
		SDL_ReleaseGPUSampler(Device, sampler);

		vertexBuffer.Dispose();
		indexBuffer.Dispose();
	}

	/// <summary>
	/// Begins a new Frame (clears / increments internal data).
	/// Call this alongside <see cref="ImGui.NewFrame"/>.
	/// </summary>
	public void NewFrame()
	{
		callbacks.Clear();
	}

	/// <summary>
	/// Adds a UserCallback method to the current Window Draw List.
	/// This wraps <see cref="ImDrawListPtr.AddCallback(nint, nint)"> as it 
	/// doesn't work gracefully with C# by default.
	/// </summary>
	public void AddUserCallback(UserCallback callback, nint? userData = null)
	{
		callbacks.Add(callback);
		ImGui.GetWindowDrawList().AddCallback(callbacks.Count, userData ?? nint.Zero);
	}

	/// <summary>
	/// Renders the ImGuiContents (calls <see cref="SDL_WaitAndAcquireGPUSwapchainTexture"/> and <see cref="ImGui.Render"/> internally)
	/// </summary>
	public void Render(SDL_FColor? clearColor = null)
	{
		var cmd = SDL_AcquireGPUCommandBuffer(Device);

		if (SDL_WaitAndAcquireGPUSwapchainTexture(cmd, Window, out var swapchain, out var width, out var height))
		{
			Render(cmd, swapchain, (int)width, (int)height, clearColor);
		}
		else
		{
			Console.WriteLine($"{nameof(SDL_WaitAndAcquireGPUSwapchainTexture)} failed: {SDL_GetError()}");
		}

		SDL_SubmitGPUCommandBuffer(cmd);
	}

	/// <summary>
	/// Renders the ImGuiContents (calls <see cref="ImGui.Render"/> internally)
	/// </summary>
	public void Render(nint cmd, nint swapchainTexture, int swapchainWidth, int swapchainHeight, SDL_FColor? clearColor = null)
	{
		// update IO values
		var io = ImGui.GetIO();
		io.DisplaySize = new Vector2(swapchainWidth, swapchainHeight) / Scale;
		io.DisplayFramebufferScale = Vector2.One * Scale;

		// render data
		ImGui.Render();

		// vaidate data
		var data = ImGui.GetDrawData();
		if (data.NativePtr == null || data.TotalVtxCount <= 0)
			return;

		// build vertex/index buffer lists
		{
			// calculate total size
			var vertexCount = 0;
			var indexCount = 0;
			for (int i = 0; i < data.CmdListsCount; i ++)
			{
				vertexCount += data.CmdLists[i].VtxBuffer.Size;
				indexCount += data.CmdLists[i].IdxBuffer.Size;
			}

			// make sure we have enough space
			if (vertexCount > vertices.Length)
				Array.Resize(ref vertices, vertexCount);
			if (indexCount > indices.Length)
				Array.Resize(ref indices, indexCount);

			// copy data to arrays
			vertexCount = indexCount = 0;
			for (int i = 0; i < data.CmdListsCount; i ++)
			{
				var list = data.CmdLists[i];
				var vertexSrc = new Span<ImDrawVert>((void*)list.VtxBuffer.Data, list.VtxBuffer.Size);
				var indexSrc = new Span<ushort>((void*)list.IdxBuffer.Data, list.IdxBuffer.Size);

				vertexSrc.CopyTo(vertices.AsSpan()[vertexCount..]);
				indexSrc.CopyTo(indices.AsSpan()[indexCount..]);

				vertexCount += vertexSrc.Length;
				indexCount += indexSrc.Length;
			}

			// begin GPU copy pass (upload buffers)
			var copy = SDL_BeginGPUCopyPass(cmd);
			vertexBuffer.Upload<ImDrawVert>(copy, vertices.AsSpan(0, vertexCount));
			indexBuffer.Upload<ushort>(copy, indices.AsSpan(0, indexCount));
			SDL_EndGPUCopyPass(copy);
		}

		// render contents
		{
			SDL_GPUColorTargetInfo colorTargetInfo = new()
			{
				texture = swapchainTexture,
				clear_color = clearColor ?? default,
				load_op = clearColor.HasValue ? SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR : SDL_GPULoadOp.SDL_GPU_LOADOP_LOAD,
				store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
			};

			scoped ref var depthTarget = ref Unsafe.NullRef<SDL_GPUDepthStencilTargetInfo>();
			var pass = SDL_BeginGPURenderPass(cmd, [ colorTargetInfo ], 1, depthTarget);

			// bind pipeline
			SDL_BindGPUGraphicsPipeline(pass, pipeline);

			// bind fragment samplers
			var texture = fontTexture;
			SDL_BindGPUFragmentSamplers(pass, 0, [new() { sampler = sampler, texture = fontTexture }], 1);

			// bind buffers
			SDL_BindGPUIndexBuffer(pass, new() { buffer = indexBuffer.Buffer }, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT);
			SDL_BindGPUVertexBuffers(pass, 0, [new() { buffer = vertexBuffer.Buffer }], 1);

			// set matrix uniform
			{
				Matrix4x4 mat =
					Matrix4x4.CreateScale(data.FramebufferScale.X, data.FramebufferScale.Y, 1.0f) *
					Matrix4x4.CreateOrthographicOffCenter(0, swapchainWidth, swapchainHeight, 0, 0, 100.0f);
				SDL_PushGPUVertexUniformData(cmd, 0, new nint(&mat), (uint)Marshal.SizeOf<Matrix4x4>());
			}

			// draw commands
			var globalVtxOffset = 0;
			var globalIdxOffset = 0;
			for (int i = 0; i < data.CmdListsCount; i ++)
			{
				var imList = data.CmdLists[i];
				var imCommands = (ImDrawCmd*)imList.CmdBuffer.Data;

				for (var imCmd = imCommands; imCmd < imCommands + imList.CmdBuffer.Size; imCmd++)
				{
					if (imCmd->UserCallback != nint.Zero)
					{
						var index = (int)imCmd->UserCallback - 1;
						if (index >= 0 && index < callbacks.Count)
							callbacks[(int)imCmd->UserCallback - 1].Invoke(imList, imCmd);
						continue;
					}

					if (imCmd->TextureId != texture)
					{
						texture = imCmd->TextureId;
						SDL_BindGPUFragmentSamplers(pass, 0, [new() { sampler = sampler, texture = fontTexture }], 1);
					}

					SDL_SetGPUScissor(pass, new()
					{
						x = (int)(imCmd->ClipRect.X * data.FramebufferScale.X),
						y = (int)(imCmd->ClipRect.Y * data.FramebufferScale.Y),
						w = (int)((imCmd->ClipRect.Z - imCmd->ClipRect.X) * data.FramebufferScale.X),
						h = (int)((imCmd->ClipRect.W - imCmd->ClipRect.Y) * data.FramebufferScale.Y),
					});

					SDL_DrawGPUIndexedPrimitives(
						render_pass: pass,
						num_indices: imCmd->ElemCount,
						num_instances: 1,
						first_index: (uint)(imCmd->IdxOffset + globalIdxOffset),
						vertex_offset: (int)(imCmd->VtxOffset + globalVtxOffset),
						first_instance: 0
					);
				}

				globalVtxOffset += imList.VtxBuffer.Size;
				globalIdxOffset += imList.IdxBuffer.Size;
			}

			SDL_EndGPURenderPass(pass);
		}
	}

	/// <summary>
	/// Wrapper around an SDL GPU Buffer, used as Vertex and Index buffer share
	/// a lot of the same details.
	/// </summary>
	private class GpuBuffer(nint device, SDL_GPUBufferUsageFlags usage) : IDisposable
	{
		public readonly nint Device = device;
		public readonly SDL_GPUBufferUsageFlags Usage = usage;
		public nint Buffer { get; private set; }
		public int Capacity { get; private set; }

		~GpuBuffer() => Dispose();

		public void Dispose()
		{
			GC.SuppressFinalize(this);

			if (Buffer != nint.Zero)
			{
				SDL_ReleaseGPUBuffer(Device, Buffer);
				Buffer = nint.Zero;
			}
		}

		public void Upload<T>(nint copyPass, in ReadOnlySpan<T> data) where T : unmanaged
		{
			var dataSize = Marshal.SizeOf<T>() * data.Length;

			// Create a new buffer if our size is too large
			if (Buffer == nint.Zero || dataSize > Capacity)
			{
				if (Buffer != nint.Zero)
				{
					SDL_ReleaseGPUBuffer(Device, Buffer);
					Buffer = nint.Zero;
				}

				Capacity = Math.Max(Capacity, 8);
				while (Capacity < dataSize)
					Capacity *= 2;

				Buffer = SDL_CreateGPUBuffer(Device, new()
				{
					usage = Usage,
					size = (uint)Capacity
				});

				if (Buffer == nint.Zero)
					throw new Exception($"{nameof(SDL_CreateGPUBuffer)} Failed: {SDL_GetError()}");
			}

			// TODO: cache this! reuse transfer buffer!
			var transferBuffer = SDL_CreateGPUTransferBuffer(Device, new()
			{
				usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
				size = (uint)dataSize
			});

			// copy data
			fixed (T* src = data)
			{
				byte* dst = (byte*)SDL_MapGPUTransferBuffer(Device, transferBuffer, false);
				System.Buffer.MemoryCopy(src, dst, dataSize, dataSize);
				SDL_UnmapGPUTransferBuffer(Device, transferBuffer);
			}

			// submit to the GPU
			SDL_UploadToGPUBuffer(copyPass,
				source: new()
				{
					transfer_buffer = transferBuffer,
					offset = 0
				},
				destination: new()
				{
					buffer = Buffer,
					offset = 0,
					size = (uint)dataSize
				},
				cycle: false
			);

			// release transfer buffer
			SDL_ReleaseGPUTransferBuffer(Device, transferBuffer);
		}
	}

	/// <summary>
	/// Shaders are stored as Embedded files (see ImGuiSDL.csproj)
	/// </summary>
	private static byte[] GetEmbeddedBytes(string file)
	{
		var assembly = typeof(ImGuiRenderer).Assembly;
		using var stream = assembly.GetManifestResourceStream(file);
		if (stream != null)
		{
			var result = new byte[stream.Length];
			stream.ReadExactly(result);
			return result;
		}

		throw new Exception($"Failed to load Embedded file '{file}'");
	}
}