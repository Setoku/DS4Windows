﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Xml.Serialization;
using DS4Windows;
using DS4WinWPF.DS4Control.Profiles.Schema.Converters;
using ExtendedXmlSerializer;
using ExtendedXmlSerializer.Configuration;
using JetBrains.Annotations;
using BooleanConverter = DS4WinWPF.DS4Control.Profiles.Schema.Converters.BooleanConverter;
using DoubleConverter = DS4WinWPF.DS4Control.Profiles.Schema.Converters.DoubleConverter;
using GuidConverter = DS4WinWPF.DS4Control.Profiles.Schema.Converters.GuidConverter;

namespace DS4WinWPF.DS4Control.Profiles.Schema
{
    public class ProfilePropertyChangedEventArgs : EventArgs
    {
        public ProfilePropertyChangedEventArgs(string propertyName, object before, object after)
        {
            PropertyName = propertyName;
            Before = before;
            After = after;
        }

        public string PropertyName { get; }

        public object Before { get; }

        public object After { get; }
    }

    /// <summary>
    ///     "New" controller profile definition.
    /// </summary>
    public class DS4WindowsProfile :
        XmlSerializable<DS4WindowsProfile>,
        IEquatable<DS4WindowsProfile>,
        INotifyPropertyChanged
    {
        /// <summary>
        ///     The <see cref="Guid"/> identifying the default (always available) profile that is always ensured to exist.
        /// </summary>
        public static readonly Guid DefaultProfileId = Guid.Parse("C74D58EA-058F-4D01-BF08-8D765CC145D1");
        
        /// <summary>
        ///     The <see cref="Guid"/> identifying a profile that indicates "no change" (for auto switching profiles).
        /// </summary>
        public static readonly Guid EmptyProfileId = Guid.Parse("D1D90BED-9D50-41D1-BAA7-049823FDBC25");

        public delegate void ProfilePropertyChangedEventHandler([CanBeNull] object sender,
            ProfilePropertyChangedEventArgs e);

        private const string FILE_EXTENSION = ".xml";

        public DS4WindowsProfile()
        {
        }

        public DS4WindowsProfile(int index) : this()
        {
            Index = index;
        }

        #region Non-persisted properties

        /// <summary>
        ///     Sanitized XML file name derived from <see cref="DisplayName" />.
        /// </summary>
        [XmlIgnore]
        public string FileName => GetValidFileName(DisplayName);

        /// <summary>
        ///     The controller slot index this profile is loaded, if applicable. Useful to speed up lookup. This value is assigned
        ///     at runtime and not persisted.
        /// </summary>
        [XmlIgnore]
        public int? Index { get; set; }

        /// <summary>
        ///     The controller ID this profile is currently attached to. This value is assigned at runtime and not persisted.
        /// </summary>
        [XmlIgnore]
        public PhysicalAddress DeviceId { get; set; }

        /// <summary>
        ///     If true, is the default profile. There can only be one.
        /// </summary>
        [XmlIgnore]
        public bool IsDefaultProfile => Equals(Id, DefaultProfileId);

        /// <summary>
        ///     If true, is an empty profile and should not be switched to.
        /// </summary>
        [XmlIgnore] 
        public bool IsEmptyProfile => Equals(Id, EmptyProfileId);

        /// <summary>
        ///     If true, this profile is linked to the current slots device' MAC/ID. This value is assigned at runtime and not
        ///     persisted.
        /// </summary>
        [XmlIgnore]
        public bool IsLinkedProfile { get; set; }

        /// <summary>
        ///     State information if an output device is active.
        /// </summary>
        [XmlIgnore]
        public bool IsOutputDeviceEnabled { get; set; }

        #endregion

        #region Persisted properties

        /// <summary>
        ///     Auto-generated unique ID for this profile.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        ///     Friendly, user-changeable name of this profile.
        /// </summary>
        public string DisplayName { get; set; } = "Default";

        public bool EnableTouchToggle { get; set; } = true;

        public ButtonMouseInfo ButtonMouseInfo { get; set; } = new();

        public GyroControlsInfo GyroControlsInfo { get; set; } = new();

        public int IdleDisconnectTimeout { get; set; } = 0;

        public bool EnableOutputDataToDS4 { get; set; } = true;

        public bool TouchpadJitterCompensation { get; set; } = true;

        public bool LowerRCOn { get; set; } = false;

        public bool TouchClickPassthru { get; set; } = false;

        public byte RumbleBoost { get; set; } = 100;

        public int RumbleAutostopTime { get; set; } = 0;

        public byte TouchSensitivity { get; set; } = 100;

        public StickDeadZoneInfo LSModInfo { get; set; } = new();

        public StickDeadZoneInfo RSModInfo { get; set; } = new();

        public TriggerDeadZoneZInfo L2ModInfo { get; set; } = new();

        public TriggerDeadZoneZInfo R2ModInfo { get; set; } = new();

        public double LSRotation { get; set; } = 0.0;

        public double RSRotation { get; set; } = 0.0;

        public double SXDeadzone { get; set; } = 0.25;

        public double SZDeadzone { get; set; } = 0.25;

        public double SXMaxzone { get; set; } = 1.0;

        public double SZMaxzone { get; set; } = 1.0;

        public double SXAntiDeadzone { get; set; } = 0.0;

        public double SZAntiDeadzone { get; set; } = 0.0;

        public double L2Sens { get; set; } = 1;

        public double R2Sens { get; set; } = 1;

        public double LSSens { get; set; } = 1;

        public double RSSens { get; set; } = 1;

        public double SXSens { get; set; } = 1;

        public double SZSens { get; set; } = 1;

        public byte TapSensitivity { get; set; } = 0;

        public bool DoubleTap { get; set; } = false;

        public int ScrollSensitivity { get; set; } = 0;

        public int TouchPadInvert { get; set; } = 0;

        public int BluetoothPollRate { get; set; } = 4;

        public StickOutputSetting LSOutputSettings { get; set; } = new();

        public StickOutputSetting RSOutputSettings { get; set; } = new();

        public TriggerOutputSettings L2OutputSettings { get; set; } = new();

        public TriggerOutputSettings R2OutputSettings { get; set; } = new();

        public string LaunchProgram { get; set; }

        public bool DisableVirtualController { get; set; } = false;

        public bool StartTouchpadOff { get; set; } = false;

        public TouchpadOutMode TouchOutMode { get; set; } = TouchpadOutMode.Mouse;

        public string SATriggers { get; set; } = "-1";

        public bool SATriggerCondition { get; set; } = true;

        public GyroOutMode GyroOutputMode { get; set; } = GyroOutMode.Controls;

        public string SAMouseStickTriggers { get; set; } = "-1";

        public bool SAMouseStickTriggerCond { get; set; } = true;

        public GyroMouseStickInfo GyroMouseStickInfo { get; set; } = new();

        public GyroDirectionalSwipeInfo GyroSwipeInfo { get; set; } = new();

        public bool GyroMouseStickToggle { get; set; } = false;

        public bool GyroMouseStickTriggerTurns { get; set; } = true;

        public int GyroMouseStickHorizontalAxis { get; set; }

        public SASteeringWheelEmulationAxisType SASteeringWheelEmulationAxis { get; set; } =
            SASteeringWheelEmulationAxisType.None;

        public int SASteeringWheelEmulationRange { get; set; } = 360;

        public int SAWheelFuzzValues { get; set; } = 0;

        public SteeringWheelSmoothingInfo WheelSmoothInfo { get; set; } = new();

        public IList<int> TouchDisInvertTriggers { get; set; } = new List<int> { -1 };

        public int GyroSensitivity { get; set; } = 100;

        public int GyroSensVerticalScale { get; set; } = 100;

        public int GyroInvert { get; set; } = 0;

        public bool GyroTriggerTurns { get; set; } = true;

        public GyroMouseInfo GyroMouseInfo { get; set; } = new();

        public int GyroMouseHorizontalAxis { get; set; } = 0;

        public bool GyroMouseToggle { get; set; } = false;

        public SquareStickInfo SquStickInfo { get; set; } = new();

        public StickAntiSnapbackInfo LSAntiSnapbackInfo { get; set; } = new();

        public StickAntiSnapbackInfo RSAntiSnapbackInfo { get; set; } = new();

        public BezierCurve LSOutCurve { get; set; } = new();

        public BezierCurve RSOutCurve { get; set; } = new();

        public BezierCurve L2OutCurve { get; set; } = new();

        public BezierCurve R2OutCurve { get; set; } = new();

        public BezierCurve SXOutCurve { get; set; } = new();

        public BezierCurve SZOutCurve { get; set; } = new();

        public bool TrackballMode { get; set; } = false;

        public double TrackballFriction { get; set; } = 10.0;

        public TouchpadAbsMouseSettings TouchPadAbsMouse { get; set; } = new();

        public TouchPadRelMouseSettings TouchPadRelMouse { get; set; } = new();

        public OutContType OutputDeviceType { get; set; } = OutContType.X360;

        public bool Ds4Mapping { get; set; } = false;

        public LightbarSettingInfo LightbarSettingInfo { get; set; } = new();

        #endregion

        public bool Equals(DS4WindowsProfile other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id.Equals(other.Id);
        }

        [CanBeNull] public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     Returns a file name from a friendly profile display name.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        /// <returns>The file system file name with extension.</returns>
        public static string GetValidFileName(string profileName)
        {
            //
            // Strip extension, if included in name
            // 
            if (profileName.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                profileName = profileName.Remove(profileName.LastIndexOf(FILE_EXTENSION,
                    StringComparison.OrdinalIgnoreCase));

            //
            // Strip invalid characters
            // 
            profileName = new string(profileName.Where(m => !Path.GetInvalidFileNameChars().Contains(m)).ToArray());

            //
            // Add extension
            // 
            return $"{profileName}{FILE_EXTENSION}";
        }

        [CanBeNull] public event ProfilePropertyChangedEventHandler ProfilePropertyChanged;

        [UsedImplicitly]
        public void OnPropertyChanged(string propertyName, object before, object after)
        {
            switch (propertyName)
            {
                //
                // Update name if this is declared an empty profile
                // 
                case nameof(Id):
                    if (Equals((Guid)after, EmptyProfileId))
                        DisplayName = "(none)";
                    break;
            }

            ProfilePropertyChanged?.Invoke(this, new ProfilePropertyChangedEventArgs(propertyName, before, after));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     Register a <see cref="ProfilePropertyChangedEventHandler"/> for this <see cref="DS4WindowsProfile"/>.
        /// </summary>
        /// <param name="handler">The <see cref="ProfilePropertyChangedEventHandler"/>.</param>
        /// <returns>This instance of <see cref="DS4WindowsProfile"/>.</returns>
        public DS4WindowsProfile WithChangeNotification([CanBeNull] ProfilePropertyChangedEventHandler handler)
        {
            ProfilePropertyChanged += (sender, args) => { handler?.Invoke(sender, args); };

            return this;
        }

        /// <summary>
        ///     Builds an absolute file system path to this <see cref="DS4WindowsProfile"/>.
        /// </summary>
        /// <param name="parentDirectory">The parent directory.</param>
        /// <returns>The resulting absolute path.</returns>
        public string GetAbsoluteFilePath(string parentDirectory)
        {
            return Path.Combine(parentDirectory, FileName);
        }

        public override IExtendedXmlSerializer GetSerializer()
        {
            return new ConfigurationContainer()
                .EnableReferences()
                .EnableMemberExceptionHandling()
                .EnableImplicitTyping(typeof(DS4WindowsProfile))
                .Type<DS4Color>().Register().Converter().Using(DS4ColorConverter.Default)
                .Type<bool>().Register().Converter().Using(BooleanConverter.Default)
                .Type<BezierCurve>().Register().Converter().Using(BezierCurveConverter.Default)
                .Type<double>().Register().Converter().Using(DoubleConverter.Default)
                .Type<Guid>().Register().Converter().Using(GuidConverter.Default)
                .Create();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DS4WindowsProfile)obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return $"{DisplayName} ({Id})";
        }
    }
}