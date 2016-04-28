﻿/*
Copyright (c) 2016, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	using System.Net;
	using ConfigurationPage.PrintLeveling;
	using SettingsDictionary = Dictionary<string, string>;
	using DataStorage;
	using Agg.PlatformAbstract;
	public class SettingsProfile
	{
		private static string configFileExtension = "slice";

		public RootedObjectEventHandler SettingsChanged = new RootedObjectEventHandler();

		public RootedObjectEventHandler DoPrintLevelingChanged = new RootedObjectEventHandler();

		private LayeredProfile layeredProfile;

		private int settingsHashCode;

		private void OnSettingsChanged()
		{
			//Set hash code back to 0
			this.settingsHashCode = 0;
			SettingsChanged.CallEvents(this, null);
		}

		public bool PrinterSelected => layeredProfile.OemProfile.OemLayer.Keys.Count > 0;

		internal SettingsProfile(LayeredProfile profile)
		{
			layeredProfile = profile;
		}

		public SettingsLayer BaseLayer => layeredProfile.BaseLayer;

		public SettingsLayer OemLayer => layeredProfile.OemProfile.OemLayer;

		public SettingsLayer UserLayer => layeredProfile.UserLayer;

		public string ActiveMaterialKey => layeredProfile.ActiveMaterialKey;

		public string ActiveQualityKey
		{
			get
			{
				return layeredProfile.ActiveQualityKey;
			}
			set
			{
				layeredProfile.ActiveQualityKey = value;
			}
		}

		public bool InBaseConfig(string name)
		{
			//Check whether the default settings (layer 0) contain a settings definition
			return BaseLayer.ContainsKey(name);
		}

		internal void RunInTransaction(Action<SettingsProfile> action)
		{
			// TODO: Implement RunInTransaction
			// Suspend writes
			action(this);
			// Commit
		}

		public IEnumerable<string> AllMaterialKeys()
		{
			return layeredProfile.AllMaterialKeys();
		}

		public IEnumerable<string> AllQualityKeys()
		{
			return layeredProfile.AllQualityKeys();
		}

		public class SettingsConverter
		{
			public static void LoadConfigurationSettingsFromFileAsUnsaved(string pathAndFileName)
			{
				try
				{
					if (File.Exists(pathAndFileName))
					{
						string[] lines = System.IO.File.ReadAllLines(pathAndFileName);
						foreach (string line in lines)
						{
							//Ignore commented lines
							if (line.Trim() != "" && !line.StartsWith("#"))
							{
								string[] settingLine = line.Split('=');
								if (settingLine.Length > 1)
								{
									string keyName = settingLine[0].Trim();
									string settingDefaultValue = settingLine[1].Trim();

									//Add the setting to the active layer
									//SaveValue(keyName, settingDefaultValue);
									throw new NotImplementedException("load to dictionary");
								}
							}
						}
					}
				}
				catch (Exception e)
				{
					Debug.Print(e.Message);
					GuiWidget.BreakInDebugger();
					Debug.WriteLine(string.Format("Error loading configuration: {0}", e));
				}
			}
		}

		public string GetExtruderTemperature(int extruderIndex)
		{
			if (extruderIndex >= layeredProfile.MaterialSettingsKeys.Count)
			{
				return null;
			}

			string materialKey = layeredProfile.MaterialSettingsKeys[extruderIndex];
			SettingsLayer layer = layeredProfile.GetMaterialLayer(materialKey);

			string result;
			layer.TryGetValue("temperature", out result);
			return result;
		}

		internal SettingsLayer GetMaterialLayer(string key)
		{
			return layeredProfile.GetMaterialLayer(key);
		}

		internal SettingsLayer GetQualityLayer(string key)
		{
			return layeredProfile.GetQualityLayer(key);
		}

		internal void SetMaterialPreset(int extruderIndex, string text)
		{
			layeredProfile.SetMaterialPreset(extruderIndex, text);
		}

		public bool HasFan()
		{
			return GetActiveValue("has_fan") == "1";
		}

		public bool CenterOnBed()
		{
			return GetActiveValue("center_part_on_bed") == "1";
		}

		public bool ShowResetConnection()
		{
			return GetActiveValue("show_reset_connection") == "1";
		}

		public bool HasHardwareLeveling()
		{
			return GetActiveValue("has_hardware_leveling") == "1";
		}

		public bool HasSdCardReader()
		{
			return GetActiveValue("has_sd_card_reader") == "1";
		}

		public double BedTemperature
		{
			get
			{
				double targetTemp = 0;
				if (HasHeatedBed())
				{
					double.TryParse(GetActiveValue("bed_temperature"), out targetTemp);
				}
				return targetTemp;
			}
		}

		/// <summary>
		/// Control the PS_ON pin via M80/81 if enabled in firmware and printer settings, allowing the printer board to toggle off the ATX power supply
		/// </summary>
		public bool HasPowerControl
		{
			get
			{
				return GetActiveValue("has_power_control") == "1";
			}
		}

		public bool HasHeatedBed()
		{
			return GetActiveValue("has_heated_bed") == "1";
		}

		public bool SupportEnabled
		{
			get
			{
				return GetActiveValue("support_material") == "1";
			}
		}

		public bool ShowFirmwareUpdater
		{
			get
			{
				return GetActiveValue("include_firmware_updater") == "Simple Arduino";
			}
		}

		public int SupportExtruder
		{
			get
			{
				return int.Parse(GetActiveValue("support_material_extruder"));
			}
		}

		public int[] LayerToPauseOn
		{
			get
			{
				string[] userValues = GetActiveValue("layer_to_pause").Split(';');

				int temp;
				return userValues.Where(v => int.TryParse(v, out temp)).Select(v =>
				{
					//Convert from 0 based index to 1 based index
					int val = int.Parse(v);

					// Special case for user entered zero that pushes 0 to 1, otherwise val = val - 1 for 1 based index
					return val == 0 ? 1 : val - 1;
				}).ToArray();
			}
		}

		public double ProbePaperWidth
		{
			get
			{
				return double.Parse(GetActiveValue("manual_probe_paper_width"));
			}
		}

		public bool RaftEnabled
		{
			get
			{
				return GetActiveValue("create_raft") == "1";
			}
		}

		public int RaftExtruder
		{
			get
			{
				return int.Parse(GetActiveValue("raft_extruder"));
			}
		}

		public double MaxFanSpeed
		{
			get
			{
				return ParseDouble(GetActiveValue("max_fan_speed"));
			}
		}

		public double FillDensity
		{
			get
			{
				string fillDensityValueString = GetActiveValue("fill_density");
				if (fillDensityValueString.Contains("%"))
				{
					string onlyNumber = fillDensityValueString.Replace("%", "");
					double ratio = ParseDouble(onlyNumber) / 100;
					return ratio;
				}
				else
				{
					return ParseDouble(GetActiveValue("fill_density"));
				}
			}
		}

		public double MinFanSpeed
		{
			get
			{
				return ParseDouble(GetActiveValue("min_fan_speed"));
			}
		}

		public double FirstLayerHeight
		{
			get
			{
				string firstLayerValueString = GetActiveValue("first_layer_height");
				if (firstLayerValueString.Contains("%"))
				{
					string onlyNumber = firstLayerValueString.Replace("%", "");
					double ratio = ParseDouble(onlyNumber) / 100;
					return LayerHeight * ratio;
				}
				double firstLayerValue;
				firstLayerValue = ParseDouble(firstLayerValueString);

				return firstLayerValue;
			}
		}

		internal string GetMaterialPresetKey(int extruderIndex)
		{
			return layeredProfile.GetMaterialPresetKey(extruderIndex);
		}

		public double FirstLayerExtrusionWidth
		{
			get
			{
				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("first_layer_extrusion_width", "notused", "nozzle_diameter");

				double firstLayerValue = ParseDouble(mapper.Value);
				return firstLayerValue;
			}
		}

		private static double ParseDouble(string firstLayerValueString)
		{
			double firstLayerValue;
			if (!double.TryParse(firstLayerValueString, out firstLayerValue))
			{
				throw new Exception(string.Format("Format cannot be parsed. FirstLayerHeight '{0}'", firstLayerValueString));
			}
			return firstLayerValue;
		}

		public double LayerHeight
		{
			get { return ParseDouble(GetActiveValue("layer_height")); }
		}

		public Vector2 GetBedSize()
		{
			return BedSize;
		}

		public Vector2 BedSize
		{
			get
			{
				return GetActiveVector2("bed_size");
			}
		}

		public MeshVisualizer.MeshViewerWidget.BedShape BedShape
		{
			get
			{
				switch (GetActiveValue("bed_shape"))
				{
					case "rectangular":
						return MeshVisualizer.MeshViewerWidget.BedShape.Rectangular;

					case "circular":
						return MeshVisualizer.MeshViewerWidget.BedShape.Circular;

					default:
#if DEBUG
						throw new NotImplementedException(string.Format("'{0}' is not a known bed_shape.", GetActiveValue("bed_shape")));
#else
                        return MeshVisualizer.MeshViewerWidget.BedShape.Rectangular;
#endif
				}
			}
		}

		public Vector2 BedCenter
		{
			get
			{
				return GetActiveVector2("print_center");
			}
		}

		public double BuildHeight
		{
			get
			{
				return ParseDouble(GetActiveValue("build_height"));
			}
		}

		public Vector2 PrintCenter
		{
			get
			{
				return GetActiveVector2("print_center");
			}
		}

		public int ExtruderCount
		{
			get
			{
				if (ExtrudersShareTemperature)
				{
					return 1;
				}

				int extruderCount;
				string extruderCountString = GetActiveValue("extruder_count");
				if (!int.TryParse(extruderCountString, out extruderCount))
				{
					return 1;
				}

				return extruderCount;
			}
		}

		public bool ExtrudersShareTemperature
		{
			get
			{
				return (int.Parse(GetActiveValue("extruders_share_temperature")) == 1);
			}
		}

		public Vector2 GetOffset(int extruderIndex)
		{
			string currentOffsets = GetActiveValue("extruder_offset");
			string[] offsets = currentOffsets.Split(',');
			int count = 0;
			foreach (string offset in offsets)
			{
				if (count == extruderIndex)
				{
					string[] xy = offset.Split('x');
					return new Vector2(double.Parse(xy[0]), double.Parse(xy[1]));
				}
				count++;
			}

			return Vector2.Zero;
		}

		public double NozzleDiameter
		{
			get { return ParseDouble(GetActiveValue("nozzle_diameter")); }
		}

		public double FilamentDiameter
		{
			get { return ParseDouble(GetActiveValue("filament_diameter")); }
		}

		public bool LevelingRequiredToPrint
		{
			get
			{
				return GetActiveValue("print_leveling_required_to_print") == "1";
			}
		}

		private PrintLevelingData printLevelingData = null;
		public PrintLevelingData PrintLevelingData
		{
			get
			{
				if (printLevelingData == null)
				{
					printLevelingData = PrintLevelingData.Create(
						layeredProfile.GetValue("MatterControl.PrintLevelingData"), 
						layeredProfile.GetValue("MatterControl.PrintLevelingProbePositions"));

					PrintLevelingPlane.Instance.SetPrintLevelingEquation(
						printLevelingData.SampledPosition0,
						printLevelingData.SampledPosition1,
						printLevelingData.SampledPosition2,
						ActiveSliceSettings.Instance.PrintCenter);
				}

				return printLevelingData;
			}
			set
			{
				printLevelingData = value;
				layeredProfile.SetActiveValue("MatterControl.PrintLevelingData", JsonConvert.SerializeObject(PrintLevelingData));
			}
		}

		public bool DoPrintLeveling
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.PrintLevelingEnabled") == "true";
			}

			set
			{
				// Early exit if already set
				if(value == this.DoPrintLeveling)
				{
					return;
				}

				layeredProfile.SetActiveValue("MatterControl.PrintLevelingEnabled", value ? "true" : "false");

				DoPrintLevelingChanged.CallEvents(this, null);

				if (value)
				{
					PrintLevelingData levelingData = ActiveSliceSettings.Instance.PrintLevelingData;
					PrintLevelingPlane.Instance.SetPrintLevelingEquation(
						levelingData.SampledPosition0,
						levelingData.SampledPosition1,
						levelingData.SampledPosition2,
						ActiveSliceSettings.Instance.PrintCenter);
				}
			}
		}

		private static readonly SlicingEngineTypes defaultEngineType = SlicingEngineTypes.MatterSlice;

		public SlicingEngineTypes ActiveSliceEngineType
		{
			get
			{
				string engineType = layeredProfile.GetValue("MatterControl.SlicingEngine");
				if (string.IsNullOrEmpty(engineType))
				{
					return defaultEngineType;
				}

				var engine = (SlicingEngineTypes)Enum.Parse(typeof(SlicingEngineTypes), engineType);
				return engine;
			}
			set
			{
				SetActiveValue("MatterControl.SlicingEngine", value.ToString());
			}
		}

		public SliceEngineMapping ActiveSliceEngine
		{
			get
			{
				switch (ActiveSliceEngineType)
				{
					case SlicingEngineTypes.CuraEngine:
						return EngineMappingCura.Instance;

					case SlicingEngineTypes.MatterSlice:
						return EngineMappingsMatterSlice.Instance;

					case SlicingEngineTypes.Slic3r:
						return Slic3rEngineMappings.Instance;

					default:
						return null;
				}
			}
		}

		///<summary>
		///Returns the first matching value discovered while enumerating the settings layers
		///</summary>
		public string GetActiveValue(string sliceSetting)
		{
			return layeredProfile.GetValue(sliceSetting);
		}

		public string GetActiveValue(string sliceSetting, IEnumerable<SettingsLayer> layers)
		{
			return layeredProfile.GetValue(sliceSetting, layers);
		}

		public void SetActiveValue(string sliceSetting, string sliceValue)
		{
			layeredProfile.SetActiveValue(sliceSetting, sliceValue);
		}

		public void SetActiveValue(string sliceSetting, string sliceValue, SettingsLayer persistenceLayer)
		{
			layeredProfile.SetActiveValue(sliceSetting, sliceValue, persistenceLayer);
		}

		public void ClearValue(string sliceSetting)
		{
			layeredProfile.ClearValue(sliceSetting);
		}

		public void ClearValue(string sliceSetting, SettingsLayer persistenceLayer)
		{
			layeredProfile.ClearValue(sliceSetting, persistenceLayer);
		}

		/// <summary>
		/// Returns whether or not the setting is overridden by the active layer
		/// </summary>
		public bool SettingExistsInLayer(string sliceSetting, NamedSettingsLayers layer)
		{
			if (layeredProfile == null)
			{
				return false;
			}

			switch (layer)
			{
				case NamedSettingsLayers.Quality:
					return layeredProfile?.QualityLayer?.ContainsKey(sliceSetting) == true;
				case NamedSettingsLayers.Material:
					return layeredProfile?.MaterialLayer?.ContainsKey(sliceSetting) == true;
				case NamedSettingsLayers.User:
					return layeredProfile?.UserLayer?.ContainsKey(sliceSetting) == true;
				default:
					return false;
			}
		}

		public Vector2 GetActiveVector2(string sliceSetting)
		{
			string[] twoValues = GetActiveValue(sliceSetting).Split(',');
			if (twoValues.Length != 2)
			{
				throw new Exception(string.Format("Not parsing {0} as a Vector2", sliceSetting));
			}
			Vector2 valueAsVector2 = new Vector2();
			valueAsVector2.x = ParseDouble(twoValues[0]);
			valueAsVector2.y = ParseDouble(twoValues[1]);
			return valueAsVector2;
		}

		public void SaveAs()
		{
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Slice Configuration".Localize() + "|*." + configFileExtension);
			saveParams.FileName = "default_settings.ini";
			FileDialog.SaveFileDialog(saveParams, onExportFileSelected);
		}

		private void onExportFileSelected(SaveFileDialogParams saveParams)
		{
			if (!string.IsNullOrEmpty(saveParams.FileName))
			{
				GenerateConfigFile(saveParams.FileName, false);
			}
		}

		public override int GetHashCode()
		{
			if (this.settingsHashCode == 0)
			{
				var bigStringForHashCode = new StringBuilder();

				foreach (var kvp in this.BaseLayer)
				{
					string activeValue = GetActiveValue(kvp.Key);
					bigStringForHashCode.Append(kvp.Key);
					bigStringForHashCode.Append(activeValue);
				}

				this.settingsHashCode = bigStringForHashCode.ToString().GetHashCode();
			}
			return this.settingsHashCode;
		}

		public void GenerateConfigFile(string fileName, bool replaceMacroValues)
		{
			using (var outstream = new StreamWriter(fileName))
			{
				foreach (var kvp in this.BaseLayer)
				{
					string activeValue = GetActiveValue(kvp.Key);
					if (replaceMacroValues)
					{
						activeValue = GCodeProcessing.ReplaceMacroValues(activeValue);
					}
					outstream.Write(string.Format("{0} = {1}\n", kvp.Key, activeValue));
					activeValue = GCodeProcessing.ReplaceMacroValues(activeValue);
				}
			}
		}

		public bool IsValid()
		{
			try
			{
				if (LayerHeight > NozzleDiameter)
				{
					string error = "'Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("Layer Height = {0}\nNozzle Diameter = {1}".Localize(), LayerHeight, NozzleDiameter);
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}
				else if (FirstLayerHeight > NozzleDiameter)
				{
					string error = "'First Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("First Layer Height = {0}\nNozzle Diameter = {1}".Localize(), FirstLayerHeight, NozzleDiameter);
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				// If we have print leveling turned on then make sure we don't have any leveling commands in the start gcode.
				if (PrinterConnectionAndCommunication.Instance.ActivePrinter.DoPrintLeveling)
				{
					string[] startGCode = GetActiveValue("start_gcode").Replace("\\n", "\n").Split('\n');
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							string error = "Start G-Code cannot contain G29 if Print Leveling is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G29 if you are planning on using print leveling. Change your start G-Code or turn off print leveling".Localize();
							string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Custom G-Code' -> 'Start G-Code'".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							string error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G30 if you are planning on using print leveling. Change your start G-Code or turn off print leveling".Localize();
							string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Custom G-Code' -> 'Start G-Code'".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}
					}
				}

				if (FirstLayerExtrusionWidth > NozzleDiameter * 4)
				{
					string error = "'First Layer Extrusion Width' must be less than or equal to the 'Nozzle Diameter' * 4.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}\nNozzle Diameter = {1}".Localize(), GetActiveValue("first_layer_extrusion_width"), NozzleDiameter);
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (FirstLayerExtrusionWidth <= 0)
				{
					string error = "'First Layer Extrusion Width' must be greater than 0.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}".Localize(), GetActiveValue("first_layer_extrusion_width"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (MinFanSpeed > 100)
				{
					string error = "The Minimum Fan Speed can only go as high as 100%.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), MinFanSpeed);
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Cooling'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (MaxFanSpeed > 100)
				{
					string error = "The Maximum Fan Speed can only go as high as 100%.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), MaxFanSpeed);
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Cooling'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (ExtruderCount < 1)
				{
					string error = "The Extruder Count must be at least 1.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), ExtruderCount);
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Features'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (FillDensity < 0 || FillDensity > 1)
				{
					string error = "The Fill Density must be between 0 and 1.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), FillDensity);
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Infill'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (FillDensity == 1
					&& GetActiveValue("infill_type") != "LINES")
				{
					string error = "Solid Infill works best when set to LINES.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), GetActiveValue("infill_type"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Infill Type'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return true;
				}


				string normalSpeedLocation = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Speed'".Localize();
				// If the given speed is part of the current slice engine then check that it is greater than 0.
				if (!ValidateGoodSpeedSettingGreaterThan0("bridge_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("external_perimeter_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("first_layer_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("gap_fill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("infill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("perimeter_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("small_perimeter_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("solid_infill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("support_material_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("top_solid_infill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("travel_speed", normalSpeedLocation)) return false;

				string retractSpeedLocation = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Filament' -> 'Retraction'".Localize();
				if (!ValidateGoodSpeedSettingGreaterThan0("retract_speed", retractSpeedLocation)) return false;
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
				string stackTraceNoBackslashRs = e.StackTrace.Replace("\r", "");
				ContactFormWindow.Open("Parse Error while slicing".Localize(), e.Message + stackTraceNoBackslashRs);
				return false;
			}

			return true;
		}

		private bool ValidateGoodSpeedSettingGreaterThan0(string speedSetting, string speedLocation)
		{
			string actualSpeedValueString = GetActiveValue(speedSetting);
			string speedValueString = actualSpeedValueString;
			if (speedValueString.EndsWith("%"))
			{
				speedValueString = speedValueString.Substring(0, speedValueString.Length - 1);
			}
			bool valueWasNumber = true;
			double speedToCheck;
			if (!double.TryParse(speedValueString, out speedToCheck))
			{
				valueWasNumber = false;
			}

			if (!valueWasNumber
				|| (ActiveSliceSettings.Instance.ActiveSliceEngine.MapContains(speedSetting)
				&& speedToCheck <= 0))
			{
				OrganizerSettingsData data = SliceSettingsOrganizer.Instance.GetSettingsData(speedSetting);
				if (data != null)
				{
					string error = string.Format("The '{0}' must be greater than 0.".Localize(), data.PresentationName);
					string details = string.Format("It is currently set to {0}.".Localize(), actualSpeedValueString);
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2} -> '{3}'", error, details, speedLocation, data.PresentationName), "Slice Error".Localize());
				}
				return false;
			}
			return true;
		}

		public bool AutoConnectFlag
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.AutoConnectFlag") == "true";
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.AutoConnectFlag", value ? "true" : "false");
			}
		}

		public string BaudRate
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.BaudRate");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.BaudRate", value);
			}
		}

		public string ComPort
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.ComPort");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.ComPort", value);
			}
		}

		public string SlicingEngine
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.SlicingEngine");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.SlicingEngine", value);
			}
		}

		public string DriverType
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.DriverType");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.DriverType", value);
			}
		}
		public string DeviceToken
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.DeviceToken");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.DeviceToken", value);
			}
		}

		public string DeviceType => layeredProfile.GetValue("MatterControl.DeviceType");

		public string Make => layeredProfile.GetValue("MatterControl.Make");

		// Rename to PrinterName
		public string Name
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.PrinterName");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.PrinterName", value);
			}
		}

		public string Id
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.PrinterID");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.PrinterID", value);
			}
		}

		public string Model => layeredProfile.GetValue("MatterControl.Model");

		public string ManualMovementSpeeds
		{
			get
			{
				return layeredProfile.GetValue("MatterControl.ManualMovementSpeeds");
			}
			set
			{
				layeredProfile.SetActiveValue("MatterControl.ManualMovementSpeeds", value);
			}
		}


		private List<string> printerDrivers = null;

		public List<string> PrinterDrivers()
		{
			if(printerDrivers == null)
			{
				printerDrivers = GetPrintDrivers();
			}

			return printerDrivers;
		}

		private List<string> GetPrintDrivers()
		{
			var drivers = new List<string>();

			//Determine what if any drivers are needed
			string infFileNames = GetActiveValue("windows_driver");
			if (!string.IsNullOrEmpty(infFileNames))
			{
				string[] fileNames = infFileNames.Split(',');
				foreach (string fileName in fileNames)
				{
					switch (OsInformation.OperatingSystem)
					{
						case OSType.Windows:

							string pathForInf = Path.GetFileNameWithoutExtension(fileName);

							// TODO: It's really unexpected that the driver gets copied to the temp folder every time a printer is setup. I'd think this only needs
							// to happen when the infinstaller is run (More specifically - move this to *after* the user clicks Install Driver)

							string infPath = Path.Combine("Drivers", pathForInf);
							string infPathAndFileToInstall = Path.Combine(infPath, fileName);

							if (StaticData.Instance.FileExists(infPathAndFileToInstall))
							{
								// Ensure the output directory exists
								string destTempPath = Path.GetFullPath(Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "inf", pathForInf));
								if (!Directory.Exists(destTempPath))
								{
									Directory.CreateDirectory(destTempPath);
								}

								string destTempInf = Path.GetFullPath(Path.Combine(destTempPath, fileName));

								// Sync each file from StaticData to the location on disk for serial drivers
								foreach (string file in StaticData.Instance.GetFiles(infPath))
								{
									using (Stream outstream = File.OpenWrite(Path.Combine(destTempPath, Path.GetFileName(file))))
									using (Stream instream = StaticData.Instance.OpenSteam(file))
									{
										instream.CopyTo(outstream);
									}
								}

								drivers.Add(destTempInf);
							}
							break;

						default:
							break;
					}
				}
			}

			return drivers;
		}

		public void GetMacros(string make, string model)
		{
			Dictionary<string, string> macroDict = new Dictionary<string, string>();
			macroDict["Lights On"] = "M42 P6 S255";
			macroDict["Lights Off"] = "M42 P6 S0";
			macroDict["Offset 0.8"] = "M565 Z0.8;\nM500";
			macroDict["Offset 0.9"] = "M565 Z0.9;\nM500";
			macroDict["Offset 1"] = "M565 Z1;\nM500";
			macroDict["Offset 1.1"] = "M565 Z1.1;\nM500";
			macroDict["Offset 1.2"] = "M565 Z1.2;\nM500";
			macroDict["Z Offset"] = "G1 Z10;\nG28;\nG29;\nG1 Z10;\nG1 X5 Y5 F4000;\nM117;";

			string defaultMacros = GetActiveValue("default_macros");
			var printerCustomCommands = new List<CustomCommands>();
			if (!string.IsNullOrEmpty(defaultMacros))
			{
				foreach (string macroName in defaultMacros.Split(','))
				{
					string macroValue;
					if (macroDict.TryGetValue(macroName.Trim(), out macroValue))
					{
						CustomCommands customMacro = new CustomCommands();
						customMacro.Name = macroName.Trim();
						customMacro.Value = macroValue;

						printerCustomCommands.Add(customMacro);
					}
				}
			}
		}
	}

	public class SettingsLayer : SettingsDictionary
	{
		public SettingsLayer() { }

		public SettingsLayer(Dictionary<string, string> settingsDictionary)
		{
			foreach(var kvp in settingsDictionary)
			{
				this[kvp.Key] = kvp.Value;
			}
		}

		public string Name { get; set; }
		public string Source { get; set; }
		public string ETag { get; set; }

		public string ValueOrDefault(string key, string defaultValue = "")
		{
			string foundValue;
			this.TryGetValue(key, out foundValue);

			return foundValue ?? defaultValue;
		}

		public string ValueOrNull(string key)
		{
			string foundValue;
			this.TryGetValue(key, out foundValue);

			return foundValue;
		}

		public static SettingsLayer LoadFromIni(TextReader reader)
		{
			var layer = new SettingsLayer();

			string line;
			while ((line = reader.ReadLine()) != null)
			{
				var segments = line.Split('=');
				if (!line.StartsWith("#") && !string.IsNullOrEmpty(line))
				{
					string key = segments[0].Trim();
					layer[key] = segments[1].Trim();
				}
			}

			return layer;
		}

		public static SettingsLayer LoadFromIni(string filePath)
		{
			var settings = from line in File.ReadAllLines(filePath)
						   let segments = line.Split('=')
						   where !line.StartsWith("#") && !string.IsNullOrEmpty(line)
						   select new
						   {
							   Key = segments[0].Trim(),
							   Value = segments[1].Trim()
						   };

			var layer = new SettingsLayer();
			foreach (var setting in settings)
			{
				layer[setting.Key] = setting.Value;
			}

			return layer;
		}
	}

	public class ProfileData
	{
		public string ActiveProfileID { get; set; }
		public List<PrinterInfo> Profiles { get; set; } = new List<PrinterInfo>();
	}

	public class PrinterInfo
	{
		public string Make { get; set; } = "Unknown";
		public string Model { get; set; } = "Unknown";
		public string Name { get; set; }
		public string ComPort { get; set; }
		public bool AutoConnectFlag { get; set; }
		public string Id { get; set; }
		public string BaudRate { get; set; }
		public string ProfileToken { get; set; }
		public string DriverType { get; internal set; }
		public string CurrentSlicingEngine { get; internal set; }

		internal void Delete()
		{
			throw new NotImplementedException();
		}
	}

}