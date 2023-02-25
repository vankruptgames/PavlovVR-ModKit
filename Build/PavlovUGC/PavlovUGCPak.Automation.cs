// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using AutomationTool;
using AutomationScripts;
using UnrealBuildTool;
using EpicGames.Core;

namespace AutomationTool
{
	[Help("Custom script.")]
	class PavlovUGCPak : BuildCommand
	{
		static public ProjectParams GetParams(BuildCommand Cmd, string ProjectFileName, UnrealTargetPlatform Platform, out FileReference PluginFile)
		{
			string VersionString = Cmd.ParseParamValue("Version", "NOVERSION");

			// Get the plugin filename
			string PluginPath = Cmd.ParseParamValue("PluginPath");
			if (PluginPath == null)
			{
				throw new AutomationException("Missing -PluginPath=... argument");
			}

			// Check it exists
			PluginFile = new FileReference(PluginPath);
			if (!FileReference.Exists(PluginFile))
			{
				throw new AutomationException("Plugin '{0}' not found", PluginFile.FullName);
			}

			string ReleaseVersion = Cmd.ParseParamValue("BasedOnReleaseVersion", "PavlovMod_1.0.0");

			string DirToCook = Cmd.ParseParamValue("CookDir");

			if (DirToCook == null)
			{
				DirToCook = "";
			}

			string MapToCook = Cmd.ParseParamValue("MapToCook");

			if (MapToCook == null)
			{
				MapToCook = "";
			}

			FileReference ProjectFile = new FileReference(ProjectFileName);
			ProjectParams Params;

			if (Platform == UnrealTargetPlatform.Linux)
			{
				Params = new ProjectParams(
				RawProjectPath: ProjectFile,
				Command: Cmd,
				ServerTargetPlatforms: new List<TargetPlatformDescriptor>() { new TargetPlatformDescriptor(Platform) },
				Build: false,
				Cook: true,
				DirectoriesToCook: new ParamList<string>() { DirToCook },
				//MapsToCook: new ParamList<string>() { MapToCook },
				Stage: true,
				Pak: true,
				Manifests: true,
				DLCIncludeEngineContent: true,
				BasedOnReleaseVersion: ReleaseVersion,
				DLCName: PluginFile.GetFileNameWithoutAnyExtensions(),
				SkipCookingEditorContent: true
				);
			}
			else
			{
				Params = new ProjectParams(
				RawProjectPath: ProjectFile,
				Command: Cmd,
				ClientTargetPlatforms: new List<TargetPlatformDescriptor>() { new TargetPlatformDescriptor(Platform)},
				Build: false,
				Cook: true,
				DirectoriesToCook: new ParamList<string>() { DirToCook },
				//MapsToCook: new ParamList<string>() { MapToCook },
				Stage: true,
				Pak: true,
				Manifests: true,
				DLCIncludeEngineContent: true,
				BasedOnReleaseVersion: ReleaseVersion,
				DLCName: PluginFile.GetFileNameWithoutAnyExtensions(),
				SkipCookingEditorContent: true
				);
			}

			Params.ValidateAndLog();
			return Params;
		}

		public override void ExecuteBuild()
		{
			int WorkingCL = -1;
			FileReference PluginFile = null;
			string ProjectFileName = ParseParamValue("Project");
			if (ProjectFileName == null)
			{
				ProjectFileName = CombinePaths(CmdEnv.LocalRoot, "Pavlov", "Pavlov.uproject");
			}

			string StageOnly = ParseParamValue("Stage");
			bool bStage = false;

			if (StageOnly != null)
			{
				if (StageOnly == "True")
				{
					bStage = true;
				}
			}

			string ModioUploader = ParseParamValue("ModioUploader");
			bool bModioUploader = false;

			if (ModioUploader != null)
			{
				if (ModioUploader == "True")
				{
					bModioUploader = true;
				}
			}

			LogInformation(ProjectFileName);

			string TargetPlatform = ParseParamValue("Platform");

			string PlatformDir = ParseParamValue("PlatformDir");

			if (PlatformDir == null)
			{
				 throw new AutomationException("PlatformDir is required");
			}

			UnrealTargetPlatform Platform = UnrealTargetPlatform.Win64;

			if (TargetPlatform == null)
			{
				 throw new AutomationException("Target Platform is required");
			}
			else if (TargetPlatform == "Windows")
			{
				Platform = UnrealTargetPlatform.Win64;
			}
			else if (TargetPlatform == "Android")
			{
				Platform = UnrealTargetPlatform.Android;
			}
			else if (TargetPlatform == "Linux")
			{
				Platform = UnrealTargetPlatform.Linux;
			}

			ProjectParams Params = GetParams(this, ProjectFileName, Platform, out PluginFile);
			
			string PlatformStageDir = Path.Combine(Params.StageDirectoryParam, TargetPlatform);
			bool bPreExistingStageDir = Directory.Exists(PlatformStageDir);			

			PluginDescriptor Plugin = PluginDescriptor.FromFile(PluginFile);

			FileReference ProjectFile = new FileReference(ProjectFileName);

			// Add Plugin to folders excluded for nativization in config file
			FileReference UserEditorIni = new FileReference(Path.Combine(Path.GetDirectoryName(ProjectFileName), "Config", "UserEditor.ini"));
			bool bPreExistingUserEditorIni = FileReference.Exists(UserEditorIni);
			if (!bPreExistingUserEditorIni)
			{
				// Expect this most of the time so we will create and clean up afterwards
				DirectoryReference.CreateDirectory(UserEditorIni.Directory);
				CommandUtils.WriteAllText(UserEditorIni.FullName, "");
			}

			const string ConfigSection = "BlueprintNativizationSettings";
			const string ConfigKey = "ExcludedFolderPaths";
			string ConfigValue = "/" + PluginFile.GetFileNameWithoutAnyExtensions() + "/";

			ConfigFile UserEditorConfig = new ConfigFile(UserEditorIni);
			ConfigFileSection BPNSection = UserEditorConfig.FindOrAddSection(ConfigSection);
			bool bUpdateConfigFile = !BPNSection.Lines.Exists(x => String.Equals(x.Key, ConfigKey, StringComparison.OrdinalIgnoreCase) && String.Equals(x.Value, ConfigValue, StringComparison.OrdinalIgnoreCase));
			if (bUpdateConfigFile)
			{
				BPNSection.Lines.Add(new ConfigLine(ConfigLineAction.Add, ConfigKey, ConfigValue));
				UserEditorConfig.Write(UserEditorIni);
			}

			Project.Cook(Params);
			if (!bPreExistingUserEditorIni)
			{
				FileReference.Delete(UserEditorIni);
			}

			Project.CopyBuildToStagingDirectory(Params);
			Project.Package(Params, WorkingCL);
			Project.Archive(Params);
			Project.Deploy(Params);
			
			//Builds to LinuxServer dir but have to pass it Linux
			if (TargetPlatform == "Linux")
			{
				TargetPlatform = "LinuxServer";
			}
			
			// Get path to where the plugin was staged
			string StagedPluginDir = Path.Combine(Params.StageDirectoryParam, TargetPlatform, Path.GetFileNameWithoutExtension(ProjectFileName), PluginFile.Directory.MakeRelativeTo(ProjectFile.Directory), "Content", "Paks", TargetPlatform);

			string TempPath = Path.Combine(Params.StageDirectoryParam, "Temp");

			CommandUtils.DeleteDirectory(TempPath);

			System.IO.Directory.Move(StagedPluginDir, TempPath);

			string StagedProjectDir = Path.Combine(Params.StageDirectoryParam, TargetPlatform);
			
			CommandUtils.DeleteDirectory(StagedProjectDir);

			string ModPath;

			if (bStage)
			{
				//Ommit the platform if its being staged, we only use the files for that platform so no need to specify them
				ModPath = Path.Combine(Params.StageDirectoryParam, PluginFile.GetFileNameWithoutAnyExtensions(), "Data");
				if (System.IO.Directory.Exists(ModPath))
				{
					CommandUtils.DeleteDirectory(ModPath);
				}

				string UGCPath = Path.Combine(Params.StageDirectoryParam, PluginFile.GetFileNameWithoutAnyExtensions());

				if (!System.IO.Directory.Exists(UGCPath))
				{
					CommandUtils.CreateDirectory(UGCPath);
				}
			}
			else
			{
				//we need to change to our specified platform so shack linux builds dont conflict w/ pc linux builds
				ModPath = Path.Combine(Params.StageDirectoryParam, PlatformDir, PluginFile.GetFileNameWithoutAnyExtensions());				
				CommandUtils.DeleteDirectory(ModPath);

				string ModPlatformDir = Path.Combine(Params.StageDirectoryParam, PlatformDir);
				if (!System.IO.Directory.Exists(ModPlatformDir))
				{
					System.IO.Directory.CreateDirectory(ModPlatformDir);
				}
			}

			System.IO.Directory.Move(TempPath, ModPath);
			CommandUtils.DeleteDirectory(TempPath);
			
			string MetaDataPath = ParseParamValue("MetaDataPath");

			if (MetaDataPath != null)
			{
				string MetaDataCopyToPath = Path.Combine(ModPath, "metadata.json");
				System.IO.File.Copy(MetaDataPath, MetaDataCopyToPath);
			}

			if (!bStage && !bModioUploader)
			{
				System.IO.Compression.ZipFile.CreateFromDirectory(ModPath, ModPath + ".zip");
			}
		}
	}
}