// Copyright 2013 Zynga Inc.
//	
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//		
//      Unless required by applicable law or agreed to in writing, software
//      distributed under the License is distributed on an "AS IS" BASIS,
//      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//      See the License for the specific language governing permissions and
//      limitations under the License.

using flash.utils;
using flash.display3D.textures;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

#if PLATFORM_MONOMAC
using MonoMac.OpenGL;
#elif PLATFORM_MONOTOUCH
using OpenTK.Graphics.ES20;
#elif PLATFORM_MONODROID
using OpenTK.Graphics.ES20;
using ProgramParameter = OpenTK.Graphics.ES20.All;
using ShaderType = OpenTK.Graphics.ES20.All;
using ActiveUniformType = OpenTK.Graphics.ES20.All;
using ShaderParameter = OpenTK.Graphics.ES20.All;
#endif


namespace flash.display3D {

	public class Program3D {

		public const int MaxUniforms = 512;

		public static bool Verbose = false;
		

		
		//
		// Methods
		//

#if OPENGL

		public Program3D(Context3D context3D)
		{
		}
		
		public void dispose() {
			deleteShaders();
		}

		private void deleteShaders ()
		{
			if (mProgramId!=0) {
				// this causes an exception EntryPointNotFound ..
				// GL.DeleteProgram (1, ref mProgramId  );
				mProgramId = 0;
			}

			if (mVertexShaderId!=0) {
				GL.DeleteShader (mVertexShaderId);
				mVertexShaderId = 0;
			}

			if (mFragmentShaderId!=0) {
				GL.DeleteShader (mFragmentShaderId);
				mFragmentShaderId = 0;
			}
		}
		
		public void uploadFromByteArray(ByteArray data, int byteArrayOffset, int startOffset, int count) {
			throw new NotImplementedException();
		}
		
		public void upload(ByteArray vertexProgram, ByteArray fragmentProgram) {

			// create array to hold sampler states
			var samplerStates = new SamplerState[Context3D.MaxSamplers];

			// convert shaders from AGAL to GLSL
			var glslVertex = AGALConverter.ConvertToGLSL(vertexProgram, null);
			var glslFragment = AGALConverter.ConvertToGLSL(fragmentProgram, samplerStates);
			// upload as GLSL
			uploadFromGLSL(glslVertex, glslFragment);
			// set sampler states from agal
			for (int i=0; i < samplerStates.Length; i++) {
				setSamplerState(i, samplerStates[i]);
			}
		}

		private string loadShaderSource (string filePath)
		{
			//var path = NSBundle.MainBundle.ResourcePath + Path.DirectorySeparatorChar + "GLSL";
			//var filePath = path + Path.DirectorySeparatorChar + name;
			using (StreamReader streamReader = new StreamReader (filePath)) {
				return streamReader.ReadToEnd ();
			}
		}

		public void uploadFromGLSLFiles (string vertexShaderName, string fragmentShaderName)
		{
			string vertexShaderSource = loadShaderSource(vertexShaderName);
			string fragmentShaderSource = loadShaderSource(fragmentShaderName); 
			uploadFromGLSL(vertexShaderSource, fragmentShaderSource);
		}

		public void uploadFromGLSL (string vertexShaderSource, string fragmentShaderSource)
		{
			// delete existing shaders
			deleteShaders ();

			if (Verbose) {
				Console.WriteLine (vertexShaderSource);
				Console.WriteLine (fragmentShaderSource);
			}

			mVertexSource = vertexShaderSource;
			mFragmentSource = fragmentShaderSource;
			
			// compiler vertex shader
			mVertexShaderId = GL.CreateShader (ShaderType.VertexShader);
			GL.ShaderSource (mVertexShaderId, vertexShaderSource);
			GL.CompileShader (mVertexShaderId);

			int shaderCompiled = 0;
			GL.GetShader (mVertexShaderId, ShaderParameter.CompileStatus, out shaderCompiled);

			if ( All.True != (All) shaderCompiled ) {
				var vertexInfoLog = GL.GetShaderInfoLog (mVertexShaderId);
				if (!string.IsNullOrEmpty (vertexInfoLog)) {
					Console.Write ("vertex: {0}", vertexInfoLog);
				}

				throw new Exception("Error compiling vertex shader: " + vertexInfoLog);
			}

			// compile fragment shader
			mFragmentShaderId = GL.CreateShader (ShaderType.FragmentShader);
			GL.ShaderSource (mFragmentShaderId, fragmentShaderSource);
			GL.CompileShader (mFragmentShaderId);

			int fragmentCompiled = 0;
			GL.GetShader (mFragmentShaderId, ShaderParameter.CompileStatus, out fragmentCompiled);

			if (All.True != (All) fragmentCompiled) {
				var fragmentInfoLog = GL.GetShaderInfoLog (mFragmentShaderId);
				if (!string.IsNullOrEmpty (fragmentInfoLog)) {
					Console.Write ("fragment: {0}", fragmentInfoLog);
				}

				throw new Exception("Error compiling fragment shader: " + fragmentInfoLog);
			}

			// create program
			mProgramId = GL.CreateProgram ();
			GL.AttachShader (mProgramId, mVertexShaderId);
			GL.AttachShader (mProgramId, mFragmentShaderId);

			// bind all attribute locations
			for (int i=0; i < Context3D.MaxAttributes; i++) {
				var name = "va" + i;
				if (vertexShaderSource.Contains(" " + name)) {
					GL.BindAttribLocation (mProgramId, i, name);
				}
			}

			// Link the program
			GL.LinkProgram (mProgramId);

			var infoLog = GL.GetProgramInfoLog (mProgramId);
			if (!string.IsNullOrEmpty (infoLog)) {
				Console.Write ("program: {0}", infoLog);
			}

			// build uniform list
			buildUniformList();
		}


		internal void Use()
		{
			// use program
			GL.UseProgram (mProgramId);
			
			// update texture units for all sampler uniforms
			foreach (var sampler in mSamplerUniforms)
			{
				if (sampler.RegCount == 1) {
					// single sampler
					GL.Uniform1(sampler.Location, sampler.RegIndex);
				} else {
					// sampler array?
					for (int i=0; i < sampler.RegCount; i++) {
						GL.Uniform1(sampler.Location + i, sampler.RegIndex + i);
					}
				}
			}

			foreach (var sampler in mAlphaSamplerUniforms)
			{
				if (sampler.RegCount == 1) {
					// single sampler
					GL.Uniform1(sampler.Location, sampler.RegIndex);
				} else {
					// sampler array?
					for (int i=0; i < sampler.RegCount; i++) {
						GL.Uniform1(sampler.Location + i, sampler.RegIndex + i);
					}
				}
			}
		}

		internal void SetPositionScale(float[] positionScale)
		{
			// update position scale
			if (mPositionScale != null)
			{
				GL.Uniform4(mPositionScale.Location, 1, positionScale); 
			}
		}

		public class Uniform
		{
			public string 				Name;
			public int 					Location;
			public ActiveUniformType	Type;
			public int 					Size;
			public int 					RegIndex;	// virtual register index
			public int 					RegCount;	// virtual register count (usually 1 except for matrices)
		}

		private void buildUniformList()
		{
			// clear internal lists
			mUniforms.Clear();
			mVertexUniformLookup  = new Uniform[MaxUniforms];
			mFragmentUniformLookup = new Uniform[MaxUniforms];
			mSamplerUniforms.Clear();
			mAlphaSamplerUniforms.Clear ();

			mSamplerUsageMask = 0;

			int numActive = 0;
			GL.GetProgram(mProgramId, ProgramParameter.ActiveUniforms, out numActive);
			for (int i=0; i < numActive; i++)
			{
				// create new uniform
				var uniform = new Uniform();

				int length;
				var name = new StringBuilder(1024);

				GL.GetActiveUniform(mProgramId, i, name.MaxCapacity, out length, out uniform.Size, out uniform.Type, name);
				uniform.Name 	 = name.ToString();

#if PLATFORM_MONOTOUCH || PLATFORM_MONOMAC
				uniform.Location = GL.GetUniformLocation (mProgramId, uniform.Name );
#elif PLATFORM_MONODROID
				uniform.Location = GL.GetUniformLocation (mProgramId, name);
#endif
				// remove array [x] from names
				int indexBracket = uniform.Name.IndexOf('[');
				if (indexBracket >= 0) {
					uniform.Name = uniform.Name.Substring(0, indexBracket);
				}

				// determine register count for uniform
				switch (uniform.Type)
				{
				case ActiveUniformType.FloatMat2: uniform.RegCount = 2; break;
				case ActiveUniformType.FloatMat3: uniform.RegCount = 3; break;
				case ActiveUniformType.FloatMat4: uniform.RegCount = 4; break;
				default:
					uniform.RegCount = 1; // 1 by default
					break;
				}

				// multiple regcount by size
				uniform.RegCount *= uniform.Size;

				// add uniform to program list
				mUniforms.Add(uniform);

				if (uniform.Name == "vcPositionScale")
				{
					mPositionScale = uniform;
				} else if (uniform.Name.StartsWith("vc"))
				{
					// vertex uniform
					uniform.RegIndex = int.Parse (uniform.Name.Substring(2));
					// store in vertex lookup table
					mVertexUniformLookup[uniform.RegIndex] = uniform;
				}
				else if (uniform.Name.StartsWith("fc"))
				{
					// fragment uniform
					uniform.RegIndex = int.Parse (uniform.Name.Substring(2));
					// store in fragment lookup table
					mFragmentUniformLookup[uniform.RegIndex] = uniform;
				}
				else if (uniform.Name.StartsWith("sampler") && !uniform.Name.EndsWith("_alpha"))
				{
					// sampler uniform
					uniform.RegIndex = int.Parse (uniform.Name.Substring(7));
					// add to list of sampler uniforms
					mSamplerUniforms.Add (uniform);

					// set sampler usage mask for this sampler uniform
					for (int reg=0; reg < uniform.RegCount; reg++) {
						mSamplerUsageMask |= (1 << (uniform.RegIndex + reg));
					}
				}
				else if (uniform.Name.StartsWith("sampler") && uniform.Name.EndsWith("_alpha"))
				{
					// sampler uniform
					int len = uniform.Name.IndexOf ("_") - 7;
					uniform.RegIndex = int.Parse (uniform.Name.Substring(7, len)) + 8;
					// add to list of sampler uniforms
					mAlphaSamplerUniforms.Add (uniform);
				}

				if (Verbose) {
					Console.WriteLine ("{0} name:{1} type:{2} size:{3} location:{4}", i, uniform.Name, uniform.Type, uniform.Size, uniform.Location);
				}
			}
		}

		public Uniform getUniform(bool isVertex, int register)
		{
			// maintain a map of register number to GLSL uniform
			if (isVertex) {
				return mVertexUniformLookup[register];
			} else {
				return mFragmentUniformLookup[register];
			}
		}

		public SamplerState getSamplerState(int sampler)
		{
			return mSamplerStates[sampler];
		}

		// sets the sampler state for a sampler when this program is used
		public void setSamplerState(int sampler, SamplerState state)
		{
			mSamplerStates[sampler] = state;
		}

		public int samplerUsageMask {
			get {return mSamplerUsageMask;}
		}

		private int 			   mVertexShaderId = 0;
		private int 			   mFragmentShaderId = 0;
		private int 			   mProgramId = 0;

		private string 			   mVertexSource;
		private string 			   mFragmentSource;

		// uniform lookup tables
		private List<Uniform>	   mUniforms = new List<Uniform>();
		private List<Uniform>      mSamplerUniforms = new List<Uniform>();
		private List<Uniform>      mAlphaSamplerUniforms = new List<Uniform> ();
		private Uniform[]		   mVertexUniformLookup;
		private Uniform[]		   mFragmentUniformLookup;
		private Uniform            mPositionScale;

		// sampler state information
		private SamplerState[]     mSamplerStates = new SamplerState[Context3D.MaxSamplers];
		private int				   mSamplerUsageMask = 0; 	

#else

		public Program3D(Context3D context3D)
		{
			throw new NotImplementedException();
		}
		
		public void dispose() {
			throw new NotImplementedException();
		}
		
		public void uploadFromByteArray(ByteArray data, int byteArrayOffset, int startOffset, int count) {
			throw new NotImplementedException();
		}
		
		public void upload(ByteArray vertexProgram, ByteArray fragmentProgram) {
			throw new NotImplementedException();
		}
		
		public int programId {
			get {
				throw new NotImplementedException();
			}
		}
		
		private void printProgramInfo (string name, int id)
		{
			throw new NotImplementedException();
		}
		
		private string loadShaderSource (string name)
		{
			throw new NotImplementedException();
		}
		
		public void uploadFromGLSLFiles (string vertexShaderName, string fragmentShaderName)
		{
			throw new NotImplementedException();
		}
		
		public void uploadFromGLSL(string vertexShaderSource, string fragmentShaderSource)
		{
			throw new NotImplementedException();
		}

#endif

	}
	
}

