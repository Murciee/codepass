// 柔和圆角界面 —— WPF, 纯代码 + 运行时解析 XAML, 无需 XAML 编译器
// 窗口为无边框 + 像素透明, 自绘圆角外壳与顶栏 (Win10 也能得到真圆角)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using MessageBox = Codepass.CodepassDialog;

namespace Codepass
{
    sealed class MainWindow : Window
    {
        readonly Config cfg;
        readonly string cfgPath;

         TextBox txtPort, txtPcIp, txtNtfyServer, txtNtfyTopic, txtMin, txtMax, txtClear, txtFilterKeywords, txtFilterRegex;
         TextBox txtSmsMaxLength;
         SecretField fldToken, fldNtfyToken;
         CheckBox chkTip, chkStartup, chkAutoLock;
        ComboBox cmbPrivacy, cmbEmailPrivacy;
        TextBox txtEmailServer, txtEmailAccount, txtEmailPoll, txtEmailKeywords, txtEmailSenders;
        SecretField fldEmailAuth;
        CheckBox chkEmail, chkEmailJunk, chkEmailNotify, chkEmailAutoCopy, chkQqPopup;
        ComboBox cmbEmailProvider;
        TextBlock emailAuthHint, emailProviderHint;
        Button btnEmailAuthHelp;
        TextBox txtEmailKeywordAdd;
        WrapPanel emailKeywordTags;
        CheckBox chkEmailSaveBody;
        TextBlock lblEmailStatus, lblEmailPreviewResult;
        TextBlock lblEmailFolders, lblEmailProcessing, lblEmailTestResult, lblStorageError, lblQqStatus;
        TextBox txtEmailSubjectPreview;
        Button btnEmailTest;
        CancellationTokenSource emailTestCancel;
        bool warnedEmailAuthUndecryptable;
        Button btnTestPc, btnTestNtfy, btnLock;
        StackPanel listHost;
        TextBlock lblCount, lblHistoryEmpty;
        TextBox txtHistorySearch;
        TextBox txtHistoryRetention;
        Button tabAllHistory, tabSmsHistory, tabEmailHistory;
        ScrollViewer historyScroll;
        string historyChannel = "all";
        readonly List<ResponsiveForm> responsiveForms = new List<ResponsiveForm>();
          Button navHistory, navGeneral, navEmail, navSecurity, navAbout;
         Grid pageSet, pageLog, pageAbout;
         ScrollViewer settingsScroll;
           Border cardNetwork, cardGeneral, cardNotify, cardSecurity, cardMore, cardEmail, cardEmailFilters, cardQqPopup;
        Border cardEmailStatus, cardNtfy, cardSmsFilters;
        Border dragBar;
        Border lockOverlay;
         PasswordBox txtUnlockPassword;
         TextBox txtUnlockPlain;
         Button btnUnlockEye;
         PasswordRevealer unlockRevealer;
         TextBlock lblUnlockError;

        Border toastBox;
        TextBlock toastText;
        DispatcherTimer toastTimer;

         Button btnGithub, btnUpdate;
         TextBlock lblAboutVersion, lblUpdateStatus;
         bool updateBusy;
         bool applying;
         bool loadingValues;
         DispatcherTimer windowSizeTimer;
         string autoPcIp = "";
         bool settingsRecoveryRequired;

         Border selectedRow;
         bool allowClose = false;
         bool closeCompleted;
         Window historyRawWindow, diagnosticsWindow;
         bool wasMinimized;

        static readonly Brush FgBody   = MakeBrush(0x1C, 0x1C, 0x1C);
        static readonly Brush AccentTx = MakeBrush(0x00, 0x78, 0xD4);
        static readonly Brush FgFaint  = MakeBrush(0x61, 0x61, 0x61);
        static readonly Brush RowHover = MakeBrush(0xF5, 0xF5, 0xF5);
        static readonly Brush RowSel   = MakeBrush(0xE9, 0xE9, 0xE9);

        static Brush MakeBrush(byte r, byte g, byte b)
        {
            SolidColorBrush b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
            b2.Freeze();
            return b2;
        }

        // ------------------------------------------------------------------
        // 界面 XAML (单引号属性, 便于放进 C# 逐字字符串)
        // ------------------------------------------------------------------
        const string UiXaml = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
      xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
      Background='Transparent'>
  <Grid.Resources>

    <!-- Fluent 2 elevation borders (Light). Absolute 0,0 -> 0,3 with ScaleY=-1:
         the first stop lands on the BOTTOM edge, the last one covers the rest. -->
    <LinearGradientBrush x:Key='ControlElevationBorder' MappingMode='Absolute' StartPoint='0,0' EndPoint='0,3'>
      <LinearGradientBrush.RelativeTransform>
        <ScaleTransform ScaleY='-1' CenterY='0.5'/>
      </LinearGradientBrush.RelativeTransform>
      <GradientStop Offset='0.33' Color='#29000000'/>
      <GradientStop Offset='1.0' Color='#0F000000'/>
    </LinearGradientBrush>

    <LinearGradientBrush x:Key='AccentElevationBorder' MappingMode='Absolute' StartPoint='0,0' EndPoint='0,3'>
      <LinearGradientBrush.RelativeTransform>
        <ScaleTransform ScaleY='-1' CenterY='0.5'/>
      </LinearGradientBrush.RelativeTransform>
      <GradientStop Offset='0.33' Color='#66000000'/>
      <GradientStop Offset='1.0' Color='#14FFFFFF'/>
    </LinearGradientBrush>

    <LinearGradientBrush x:Key='TextControlElevationBorder' MappingMode='Absolute' StartPoint='0,0' EndPoint='0,2'>
      <LinearGradientBrush.RelativeTransform>
        <ScaleTransform ScaleY='-1' CenterY='0.5'/>
      </LinearGradientBrush.RelativeTransform>
      <GradientStop Offset='0.5' Color='#72000000'/>
      <GradientStop Offset='1.0' Color='#0F000000'/>
    </LinearGradientBrush>

    <LinearGradientBrush x:Key='CircleElevationBorder' MappingMode='RelativeToBoundingBox'
                         StartPoint='0,0' EndPoint='0,1'>
      <GradientStop Offset='0.5' Color='#0F000000'/>
      <GradientStop Offset='0.7' Color='#29000000'/>
    </LinearGradientBrush>

    <SolidColorBrush x:Key='TextPrimary' Color='#E4000000'/>
    <SolidColorBrush x:Key='TextSecondary' Color='#9E000000'/>
    <SolidColorBrush x:Key='TextTertiary' Color='#72000000'/>
    <SolidColorBrush x:Key='CardFill' Color='#B3FFFFFF'/>
    <SolidColorBrush x:Key='StrokeDefault' Color='#0F000000'/>
    <SolidColorBrush x:Key='ControlFill' Color='#B3FFFFFF'/>
    <SolidColorBrush x:Key='SubtleHover' Color='#09000000'/>
    <SolidColorBrush x:Key='AccentFill' Color='#0078D4'/>

    <!-- WinUI focus rect: 2px FocusStrokeColorOuter (#E4000000), no glow. -->
    <Style x:Key='KeyboardFocus' TargetType='Control'>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Control'>
            <Border BorderBrush='#E4000000' BorderThickness='2' CornerRadius='6' Margin='-3'/>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style TargetType='CheckBox'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='CheckBox'>
            <StackPanel Orientation='Horizontal'>
              <Grid Width='40' Height='20' VerticalAlignment='Center'>
                <Rectangle x:Name='TrackOff' Width='40' Height='20' RadiusX='10' RadiusY='10'
                           Fill='#06000000' Stroke='#72000000' StrokeThickness='1'/>
                <Rectangle x:Name='TrackOn' Width='40' Height='20' RadiusX='10' RadiusY='10'
                           Fill='#0078D4' Opacity='0'/>
                <Grid x:Name='KnobHost' Width='20' Height='20' HorizontalAlignment='Left'>
                  <Grid.RenderTransform>
                    <TranslateTransform x:Name='KnobShift' X='0'/>
                  </Grid.RenderTransform>
                  <Ellipse x:Name='Knob' Width='12' Height='12' Fill='#9E000000'
                           Stroke='{StaticResource CircleElevationBorder}' StrokeThickness='1'
                           HorizontalAlignment='Center' VerticalAlignment='Center'
                           RenderTransformOrigin='0.5,0.5'>
                    <Ellipse.RenderTransform>
                      <ScaleTransform x:Name='KnobScale' ScaleX='1' ScaleY='1'/>
                    </Ellipse.RenderTransform>
                    <Ellipse.Effect>
                      <DropShadowEffect BlurRadius='3' ShadowDepth='1' Direction='270'
                                        Opacity='0.25' Color='#000000'/>
                    </Ellipse.Effect>
                  </Ellipse>
                </Grid>
              </Grid>
              <ContentPresenter Margin='10,0,0,0' VerticalAlignment='Center'/>
            </StackPanel>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='TrackOff' Property='Fill' Value='#0F000000'/>
                <Setter TargetName='TrackOn' Property='Fill' Value='#1984D7'/>
                <Trigger.EnterActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1.1667'/>
                      </DoubleAnimationUsingKeyFrames>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleY'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1.1667'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1'/>
                      </DoubleAnimationUsingKeyFrames>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleY'>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.ExitActions>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='TrackOff' Property='Fill' Value='#18000000'/>
                <Setter TargetName='TrackOn' Property='Fill' Value='#3090DA'/>
                <Trigger.EnterActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1.1667'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1.4167'/>
                      </DoubleAnimationUsingKeyFrames>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleY'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1.1667'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1.4167'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1.1667'/>
                      </DoubleAnimationUsingKeyFrames>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobScale'
                                                     Storyboard.TargetProperty='ScaleY'>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1.1667'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.ExitActions>
              </Trigger>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='TrackOn' Property='Opacity' Value='1'/>
                <Setter TargetName='Knob' Property='Fill' Value='#FFFFFF'/>
                <Setter TargetName='KnobHost' Property='HorizontalAlignment' Value='Right'/>
                <Trigger.EnterActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobShift'
                                                     Storyboard.TargetProperty='X'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='-20'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.167' KeySpline='0,0,0,1' Value='0'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='KnobShift'
                                                     Storyboard.TargetProperty='X'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='20'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.167' KeySpline='0,0,0,1' Value='0'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.ExitActions>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='TrackOff' Property='Fill' Value='#00FFFFFF'/>
                <Setter TargetName='Knob' Property='Fill' Value='#5C000000'/>
                <Setter Property='Opacity' Value='0.6'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style TargetType='ScrollViewer'>
      <Setter Property='PanningMode' Value='VerticalOnly'/>
      <Setter Property='HorizontalScrollBarVisibility' Value='Disabled'/>
    </Style>

    <Style x:Key='Card' TargetType='Border'>
      <Setter Property='Background' Value='#B3FFFFFF'/>
      <Setter Property='BorderBrush' Value='#0F000000'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='8'/>
      <Setter Property='Padding' Value='20,18,20,14'/>
      <Setter Property='Margin' Value='0,0,0,14'/>
    </Style>

    <Style x:Key='RecordCard' BasedOn='{StaticResource Card}' TargetType='Border'>
      <Setter Property='Padding' Value='18,18,18,12'/>
      <Setter Property='Margin' Value='22,18,22,14'/>
    </Style>

    <Style x:Key='CardTitle' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Margin' Value='0,0,0,10'/>
    </Style>

    <Style x:Key='FieldLabel' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#9E000000'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>

    <Style x:Key='Input' TargetType='TextBox'>
      <Setter Property='Height' Value='32'/>
       <Setter Property='Padding' Value='6,0,7,0'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='CaretBrush' Value='#E4000000'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='TextBox'>
                    <Grid>
              <Border x:Name='Bd' Background='#B3FFFFFF' BorderBrush='{StaticResource TextControlElevationBorder}'
                      BorderThickness='1' CornerRadius='4'/>
              <Border x:Name='Underline' Height='2' VerticalAlignment='Bottom' Margin='1,0,1,0'
                      Background='#0078D4' CornerRadius='0,0,3,3' RenderTransformOrigin='0,0.5'>
                <Border.RenderTransform>
                  <ScaleTransform x:Name='UnderlineScale' ScaleX='0'/>
                </Border.RenderTransform>
              </Border>
              <ScrollViewer x:Name='PART_ContentHost' Focusable='False' Margin='{TemplateBinding Padding}'
                            VerticalScrollBarVisibility='{TemplateBinding VerticalScrollBarVisibility}'
                            HorizontalScrollBarVisibility='{TemplateBinding HorizontalScrollBarVisibility}'
                            VerticalAlignment='Stretch'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#80F9F9F9'/>
              </Trigger>
              <Trigger Property='IsKeyboardFocusWithin' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#FFFFFF'/>
                <Trigger.EnterActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='UnderlineScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='0'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.167' KeySpline='0,0,0,1' Value='1'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='UnderlineScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='0'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.ExitActions>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='Bd' Property='Background' Value='#4DF9F9F9'/>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='EmailFieldLabel' TargetType='TextBlock' BasedOn='{StaticResource FieldLabel}'>
      <Setter Property='Height' Value='32'/>
      <Setter Property='Margin' Value='0,8,12,8'/>
      <Setter Property='VerticalAlignment' Value='Top'/>
      <Setter Property='Padding' Value='0,6,0,0'/>
    </Style>

    <Style x:Key='PasswordInput' TargetType='PasswordBox'>
      <Setter Property='Height' Value='32'/>
       <Setter Property='Padding' Value='6,0,7,0'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='CaretBrush' Value='#E4000000'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='PasswordBox'>
            <Grid>
              <Border x:Name='Bd' Background='#B3FFFFFF' BorderBrush='{StaticResource TextControlElevationBorder}'
                      BorderThickness='1' CornerRadius='4'/>
              <Border x:Name='Underline' Height='2' VerticalAlignment='Bottom' Margin='1,0,1,0'
                      Background='#0078D4' CornerRadius='0,0,3,3' RenderTransformOrigin='0,0.5'>
                <Border.RenderTransform>
                  <ScaleTransform x:Name='UnderlineScale' ScaleX='0'/>
                </Border.RenderTransform>
              </Border>
              <ScrollViewer x:Name='PART_ContentHost' Focusable='False' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#80F9F9F9'/>
              </Trigger>
              <Trigger Property='IsKeyboardFocusWithin' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#FFFFFF'/>
                <Trigger.EnterActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='UnderlineScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='0'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.167' KeySpline='0,0,0,1' Value='1'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='UnderlineScale'
                                                     Storyboard.TargetProperty='ScaleX'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='0'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.ExitActions>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='FlyoutMenu' TargetType='ContextMenu'>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='OverridesDefaultStyle' Value='True'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='ContextMenu'>
            <Grid>
              <Border Background='#F9F9F9' BorderBrush='#0F000000' BorderThickness='1' CornerRadius='8' MinWidth='160'>
                <Border.Effect>
                  <DropShadowEffect BlurRadius='16' ShadowDepth='2' Direction='270' Opacity='0.18' Color='#000000'/>
                </Border.Effect>
              </Border>
              <Border Background='#F9F9F9' BorderBrush='#0F000000' BorderThickness='1'
                      CornerRadius='8' Padding='4' MinWidth='160'>
                <ItemsPresenter/>
              </Border>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='FlyoutItem' TargetType='MenuItem'>
      <Setter Property='Padding' Value='11,7'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='MenuItem'>
            <Border x:Name='Bd' Background='Transparent' CornerRadius='4'>
              <ContentPresenter ContentSource='Header' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsHighlighted' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#0F000000'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='PrimaryBtn' TargetType='Button'>
      <Setter Property='Height' Value='32'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Padding' Value='11,5,11,6'/>
      <Setter Property='Foreground' Value='#FFFFFF'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Grid>
              <Border x:Name='Fill' Background='#0078D4' CornerRadius='4'/>
              <Border x:Name='Stroke' BorderBrush='{StaticResource AccentElevationBorder}'
                      BorderThickness='1' CornerRadius='4'/>
              <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Trigger.EnterActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='Fill'
                                                     Storyboard.TargetProperty='Opacity'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='0.9'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='Fill'
                                                     Storyboard.TargetProperty='Opacity'>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='1'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.ExitActions>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Stroke' Property='BorderBrush' Value='#00FFFFFF'/>
                <Setter Property='Foreground' Value='#B3FFFFFF'/>
                <Trigger.EnterActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='Fill'
                                                     Storyboard.TargetProperty='Opacity'>
                        <DiscreteDoubleKeyFrame KeyTime='0' Value='1'/>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='0.8'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                  <BeginStoryboard>
                    <Storyboard>
                      <DoubleAnimationUsingKeyFrames Storyboard.TargetName='Fill'
                                                     Storyboard.TargetProperty='Opacity'>
                        <SplineDoubleKeyFrame KeyTime='0:0:0.083' KeySpline='0,0,0,1' Value='0.9'/>
                      </DoubleAnimationUsingKeyFrames>
                    </Storyboard>
                  </BeginStoryboard>
                </Trigger.ExitActions>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='Fill' Property='Background' Value='#37000000'/>
                <Setter TargetName='Stroke' Property='BorderBrush' Value='#00FFFFFF'/>
                <Setter Property='Foreground' Value='#87FFFFFF'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='SecondaryBtn' TargetType='Button'>
      <Setter Property='Height' Value='32'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Padding' Value='11,5,11,6'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Grid>
              <Border x:Name='Fill' Background='#B3FFFFFF' CornerRadius='4'/>
              <Border x:Name='Stroke' BorderBrush='{StaticResource ControlElevationBorder}'
                      BorderThickness='1' CornerRadius='4'/>
              <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Fill' Property='Background' Value='#80F9F9F9'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Fill' Property='Background' Value='#4DF9F9F9'/>
                <Setter TargetName='Stroke' Property='BorderBrush' Value='#0F000000'/>
                <Setter Property='Foreground' Value='#9E000000'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='Fill' Property='Background' Value='#4DF9F9F9'/>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='PrivacyCombo' TargetType='ComboBox'>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Background' Value='#B3FFFFFF'/>
      <Setter Property='BorderBrush' Value='{StaticResource ControlElevationBorder}'/>
      <Setter Property='BorderThickness' Value='1'/>
       <Setter Property='Padding' Value='7,0,30,0'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='ComboBox'>
            <Grid>
              <ToggleButton x:Name='Toggle' Focusable='False' Background='Transparent' BorderThickness='0'
                            IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'
                            ClickMode='Press'>
                <ToggleButton.Template>
                  <ControlTemplate TargetType='ToggleButton'>
                    <ContentPresenter/>
                  </ControlTemplate>
                </ToggleButton.Template>
                <Border x:Name='Box' Background='{TemplateBinding Background}'
                        BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'
                         CornerRadius='4'>
                  <Grid>
                    <ContentPresenter Margin='{TemplateBinding Padding}' VerticalAlignment='Center'
                                      Content='{TemplateBinding SelectionBoxItem}'
                                      ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                                      ContentStringFormat='{TemplateBinding SelectionBoxItemStringFormat}'/>
                    <TextBlock Text='&#xE70D;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='11'
                               Foreground='#9E000000' HorizontalAlignment='Right' VerticalAlignment='Center'
                               Margin='0,0,10,0' IsHitTestVisible='False'/>
                  </Grid>
                </Border>
              </ToggleButton>
              <Popup x:Name='PART_Popup' AllowsTransparency='True' Focusable='False'
                     Placement='Bottom' PlacementTarget='{Binding RelativeSource={RelativeSource TemplatedParent}}'
                     IsOpen='{TemplateBinding IsDropDownOpen}' PopupAnimation='Fade'>
                <Grid>
                  <!-- 阴影层与内容层分开, 选项文字才不会被 Effect 栅格化 -->
                  <Border Background='#F9F9F9' BorderBrush='#0F000000' BorderThickness='1' CornerRadius='8'
                          MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'>
                    <Border.Effect>
                      <DropShadowEffect BlurRadius='16' ShadowDepth='2' Direction='270' Opacity='0.18' Color='#000000'/>
                    </Border.Effect>
                  </Border>
                  <Border Background='#F9F9F9' BorderBrush='#0F000000' BorderThickness='1'
                          CornerRadius='8' Padding='4' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'>
                    <ScrollViewer MaxHeight='240' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto'>
                      <ItemsPresenter/>
                    </ScrollViewer>
                  </Border>
                </Grid>
              </Popup>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger SourceName='Toggle' Property='IsMouseOver' Value='True'>
                <Setter TargetName='Box' Property='Background' Value='#80F9F9F9'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='Box' Property='Background' Value='#4DF9F9F9'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='PrivacyItem' TargetType='ComboBoxItem'>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Padding' Value='12,5,11,7'/>
      <Setter Property='MinHeight' Value='32'/>
      <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='ComboBoxItem'>
            <Grid>
              <Border x:Name='ItemBorder' Background='Transparent' CornerRadius='4'
                      Padding='{TemplateBinding Padding}'>
                <ContentPresenter VerticalAlignment='Center'/>
              </Border>
              <Border x:Name='Pill' Width='3' Height='16' CornerRadius='2' Background='#0078D4'
                      HorizontalAlignment='Left' VerticalAlignment='Center' Margin='3,0,0,0' Opacity='0'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='ItemBorder' Property='Background' Value='#09000000'/>
              </Trigger>
              <Trigger Property='IsSelected' Value='True'>
                <Setter TargetName='ItemBorder' Property='Background' Value='#06000000'/>
                <Setter TargetName='Pill' Property='Opacity' Value='1'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='NavTab' TargetType='RadioButton'>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='RadioButton'>
            <Grid Margin='6,0,0,0'>
              <Border x:Name='Bd' Background='Transparent' CornerRadius='4' Padding='14,6'>
                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
              </Border>
              <Border x:Name='Indicator' Height='3' CornerRadius='1.5' Background='#0078D4'
                      HorizontalAlignment='Stretch' VerticalAlignment='Bottom' Margin='14,0,14,1' Opacity='0'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#09000000'/>
              </Trigger>
              <Trigger Property='IsChecked' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#06000000'/>
                <Setter TargetName='Indicator' Property='Opacity' Value='1'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='NavMenuBtn' TargetType='Button'>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Grid Margin='6,0,0,0'>
              <Border x:Name='Bd' Background='Transparent' CornerRadius='4' Padding='14,6'>
                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
              </Border>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#09000000'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#0F000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

     <Style x:Key='SideNavBtn' TargetType='Button'>
       <Setter Property='Height' Value='44'/>
      <Setter Property='HorizontalContentAlignment' Value='Left'/>
       <Setter Property='Padding' Value='16,0'/>
       <Setter Property='Margin' Value='0,3'/>
       <Setter Property='FontSize' Value='15'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='Bd' Background='Transparent' CornerRadius='4'>
              <ContentPresenter Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#0F000000'/>
              </Trigger>
              <Trigger Property='Tag' Value='On'>
                <Setter TargetName='Bd' Property='Background' Value='#18000000'/>
                <Setter Property='FontWeight' Value='SemiBold'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='HistoryTab' TargetType='Button' BasedOn='{StaticResource SideNavBtn}'>
      <Setter Property='Height' Value='36'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Padding' Value='12,0'/>
      <Setter Property='Margin' Value='0,0,8,12'/>
      <Style.Triggers>
        <Trigger Property='Tag' Value='On'>
          <Setter Property='Foreground' Value='#0078D4'/>
        </Trigger>
      </Style.Triggers>
    </Style>

    <Style x:Key='CaptionBtn' TargetType='Button'>
      <Setter Property='Width' Value='42'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='FontFamily' Value='Segoe Fluent Icons, Segoe MDL2 Assets'/>
      <Setter Property='FontSize' Value='10'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='Bd' Background='Transparent' CornerRadius='4'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#09000000'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#0F000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='CloseBtn' TargetType='Button'>
      <Setter Property='Width' Value='42'/>
      <Setter Property='Height' Value='30'/>
      <Setter Property='FontFamily' Value='Segoe Fluent Icons, Segoe MDL2 Assets'/>
      <Setter Property='FontSize' Value='10'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='Bd' Background='Transparent' CornerRadius='4'>
              <ContentPresenter x:Name='Cp' HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#E81123'/>
                <Setter TargetName='Cp' Property='TextElement.Foreground' Value='#FFFFFF'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#C50F1F'/>
                <Setter TargetName='Cp' Property='TextElement.Foreground' Value='#FFFFFF'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='Hint' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='#9E000000'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>

    <Style x:Key='TableHeader' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12.5'/>
      <Setter Property='Foreground' Value='#9E000000'/>
      <Setter Property='HorizontalAlignment' Value='Center'/>
      <Setter Property='TextAlignment' Value='Center'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>

  </Grid.Resources>

  <!-- 投影层: 只画阴影, 不含任何内容 —— 内容层不套 Effect, 文字才不会被整体栅格化变糊 -->
  <Border x:Name='shellShadow' Margin='6' CornerRadius='8' Background='#F3F3F3' IsHitTestVisible='False'>
    <Border.Effect>
      <DropShadowEffect BlurRadius='20' ShadowDepth='2' Opacity='0.12' Color='#1A1A1A'/>
    </Border.Effect>
  </Border>

  <!-- 圆角外壳 (窗口无边框, 外层留白放阴影) -->
  <Border x:Name='shell' Margin='6' CornerRadius='8' Background='#F3F3F3'
          BorderBrush='#0F000000' BorderThickness='1'>

    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height='58'/>
        <RowDefinition Height='*'/>
      </Grid.RowDefinitions>

      <!-- 顶栏: 标题(可拖动) + 窗口按钮 -->
      <Border x:Name='dragBar' Grid.Row='0' Background='Transparent'
              BorderBrush='#0F000000' BorderThickness='0,0,0,1'>
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width='*'/>
            <ColumnDefinition Width='Auto'/>
          </Grid.ColumnDefinitions>

           <StackPanel Grid.Column='0' Orientation='Horizontal' VerticalAlignment='Center' Height='58' Margin='18,0,0,0'>
             <Image x:Name='imgAppLogo' Width='24' Height='24' Stretch='Uniform' VerticalAlignment='Center' Margin='0,0,8,0'/>
             <TextBlock Text='codepass' FontSize='14' FontWeight='SemiBold' Foreground='#E4000000' VerticalAlignment='Center' Margin='0,1,0,0'/>
           </StackPanel>

          <StackPanel Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center' Margin='6,0,8,0'>
            <Button x:Name='btnMin' Style='{StaticResource CaptionBtn}' Content='&#xE921;'/>
            <Button x:Name='btnClose' Style='{StaticResource CloseBtn}' Content='&#xE8BB;'/>
          </StackPanel>
        </Grid>
      </Border>

      <!-- 内容区 -->
      <Grid Grid.Row='1'>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width='178'/>
          <ColumnDefinition Width='*'/>
        </Grid.ColumnDefinitions>

        <Border Grid.Column='0' Background='#F3F3F3' BorderBrush='#0F000000' BorderThickness='0,0,1,0' Padding='8,18'>
          <StackPanel>
            <Button x:Name='navHistory' Style='{StaticResource SideNavBtn}' Content='记录'/>
            <Button x:Name='navGeneral' Style='{StaticResource SideNavBtn}' Content='短信设置'/>
            <Button x:Name='navEmail' Style='{StaticResource SideNavBtn}' Content='邮箱设置'/>
            <Button x:Name='navSecurity' Style='{StaticResource SideNavBtn}' Content='隐私设置'/>
            <Button x:Name='navAbout' Style='{StaticResource SideNavBtn}' Content='关于'/>
          </StackPanel>
        </Border>

        <!-- 设置页 -->
        <Grid x:Name='pageSet' Grid.Column='1' Visibility='Collapsed'>
          <ScrollViewer x:Name='settingsScroll' VerticalScrollBarVisibility='Auto'>
            <StackPanel Margin='22,20,22,22'>

              <Border Background='#FFF4CE' CornerRadius='6' Padding='10' Margin='0,0,0,12' x:Name='storageErrorBanner' Visibility='Collapsed'>
                <StackPanel>
                  <TextBlock x:Name='lblStorageError' TextWrapping='Wrap' Foreground='#8A4B08' FontSize='12'/>
                  <Button x:Name='btnStorageRetry' Style='{StaticResource SecondaryBtn}' Content='重试保存与邮件检查' HorizontalAlignment='Left' Margin='0,8,0,0'/>
                </StackPanel>
              </Border>

               <Border x:Name='cardEmailStatus' Style='{StaticResource Card}' Visibility='Collapsed'>
                 <StackPanel>
                   <TextBlock Style='{StaticResource CardTitle}' Text='邮箱实时状态'/>
                   <Border Background='#E8F2FC' CornerRadius='6' Padding='12'>
                     <TextBlock x:Name='lblEmailStatus' Foreground='#005A9E' FontSize='14' FontWeight='SemiBold' TextWrapping='Wrap' Text='尚未连接'/>
                   </Border>
                   <TextBlock x:Name='lblEmailFolders' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,8,0,0'/>
                   <TextBlock x:Name='lblEmailProcessing' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,5,0,0'/>
                   <WrapPanel Margin='0,12,0,0'>
                     <Button x:Name='btnEmailTest' Style='{StaticResource SecondaryBtn}' Content='测试邮箱连接' Margin='0,0,8,6'/>
                     <Button x:Name='btnEmailRecheck' Style='{StaticResource SecondaryBtn}' Content='重新检查近 30 分钟' Margin='0,0,8,6'/>
                   </WrapPanel>
                   <TextBlock x:Name='lblEmailTestResult' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,5,0,0'/>
                 </StackPanel>
               </Border>

               <Border x:Name='cardGeneral' Style='{StaticResource Card}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource CardTitle}' Text='电脑端接收'/>
                  <Grid>
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width='150'/>
                      <ColumnDefinition Width='*'/>
                    </Grid.ColumnDefinitions>
                     <Grid.RowDefinitions>
                       <RowDefinition Height='42'/>
                       <RowDefinition Height='42'/>
                       <RowDefinition Height='42'/>
                     </Grid.RowDefinitions>

                    <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='监听端口'/>
                    <TextBox x:Name='txtPort' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}' Width='100' HorizontalAlignment='Left'/>

                     <TextBlock Grid.Row='1' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='访问令牌'/>
                     <StackPanel x:Name='tokenHost' Grid.Row='1' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'/>

                     <CheckBox x:Name='chkStartup' Grid.Row='2' Grid.Column='1'
                               Content='随 Windows 静默启动 codepass（驻留托盘）'
                               Foreground='#E4000000' FontSize='14' VerticalAlignment='Center'/>
                  </Grid>
                  <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,10,0,0'
                             Text='局域网接收必须设置随机访问令牌；未设置时只监听本机。更改令牌有无、绑定地址或端口后请重启程序。'/>
                </StackPanel>
              </Border>

              <Border x:Name='cardNetwork' Style='{StaticResource Card}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource CardTitle}' Text='手机连接与验证码处理'/>
                  <Grid>
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width='150'/>
                      <ColumnDefinition Width='*'/>
                    </Grid.ColumnDefinitions>
                     <Grid.RowDefinitions>
                      <RowDefinition Height='42'/>
                      <RowDefinition Height='42'/>
                      <RowDefinition Height='42'/>
                      <RowDefinition Height='42'/>
                     </Grid.RowDefinitions>

                    <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='电脑 IPv4 地址'/>
                    <WrapPanel Grid.Row='0' Grid.Column='1' VerticalAlignment='Center'>
                       <TextBox x:Name='txtPcIp' Style='{StaticResource Input}' Width='300'/>
                       <Button x:Name='btnDetect' Style='{StaticResource SecondaryBtn}' Content='检测本机地址' Margin='8,0,0,6'/>
                       <Button x:Name='btnTestPc' Style='{StaticResource SecondaryBtn}' Content='测试连通性' Margin='8,0,0,6'/>
                    </WrapPanel>

                    <TextBlock Grid.Row='1' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='验证码长度'/>
                    <StackPanel Grid.Row='1' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'>
                      <TextBox x:Name='txtMin' Style='{StaticResource Input}' Width='96'/>
                      <TextBlock Text='–' Foreground='#9E000000' Margin='10,0' VerticalAlignment='Center'/>
                      <TextBox x:Name='txtMax' Style='{StaticResource Input}' Width='96'/>
                    </StackPanel>

                    <TextBlock Grid.Row='2' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='自动清除剪贴板'/>
                    <StackPanel Grid.Row='2' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'>
                      <TextBox x:Name='txtClear' Style='{StaticResource Input}' Width='96'/>
                      <TextBlock Style='{StaticResource Hint}' Text='秒（0 表示不清除）' Margin='10,0,0,0'/>
                    </StackPanel>

                    <TextBlock Grid.Row='3' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='桌面通知'/>
                     <CheckBox x:Name='chkTip' Grid.Row='3' Grid.Column='1' Content='收到验证码后显示通知' Foreground='#E4000000' FontSize='14' VerticalAlignment='Center'/>
                   </Grid>
                  </StackPanel>
                </Border>
                <Border x:Name='cardNtfy' Style='{StaticResource Card}'>
                  <StackPanel>
                   <TextBlock Style='{StaticResource CardTitle}' Text='公网中转（ntfy，可选）'/>
                   <Grid>
                     <Grid.ColumnDefinitions>
                       <ColumnDefinition Width='150'/>
                       <ColumnDefinition Width='*'/>
                     </Grid.ColumnDefinitions>
                     <Grid.RowDefinitions>
                       <RowDefinition Height='42'/>
                       <RowDefinition Height='42'/>
                       <RowDefinition Height='42'/>
                     </Grid.RowDefinitions>

                     <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='服务器地址'/>
                     <WrapPanel Grid.Row='0' Grid.Column='1' VerticalAlignment='Center'>
                         <TextBox x:Name='txtNtfyServer' Style='{StaticResource Input}' Width='300'/>
                        <Button x:Name='btnTestNtfy' Style='{StaticResource SecondaryBtn}' Content='测试连通性' Margin='8,0,0,6'/>
                     </WrapPanel>

                     <TextBlock Grid.Row='1' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='主题名称'/>
                     <WrapPanel Grid.Row='1' Grid.Column='1' VerticalAlignment='Center'>
                         <TextBox x:Name='txtNtfyTopic' Style='{StaticResource Input}' Width='300'/>
                        <TextBlock Style='{StaticResource Hint}' Text='留空表示停用' Margin='10,0,0,0'/>
                     </WrapPanel>

                     <TextBlock Grid.Row='2' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='访问令牌'/>
                      <StackPanel x:Name='ntfyTokenHost' Grid.Row='2' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'/>
                    </Grid>
                  </StackPanel>
                </Border>
                <Border x:Name='cardSmsFilters' Style='{StaticResource Card}'>
                  <StackPanel>
                    <TextBlock Style='{StaticResource CardTitle}' Text='短信过滤规则（可选）'/>
                    <Grid Margin='0,0,0,12'>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='150'/>
                        <ColumnDefinition Width='*'/>
                      </Grid.ColumnDefinitions>
                      <TextBlock Grid.Column='0' Style='{StaticResource FieldLabel}' Text='短信长度上限' VerticalAlignment='Top' Margin='0,8,0,0'/>
                      <StackPanel Grid.Column='1'>
                        <StackPanel Orientation='Horizontal'>
                          <TextBox x:Name='txtSmsMaxLength' Style='{StaticResource Input}' Width='96'/>
                          <TextBlock Style='{StaticResource Hint}' Text='字符（1～10000）' Margin='10,0,0,0'/>
                        </StackPanel>
                        <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,6,0,0'
                                   Text='推荐 500 字符；超过上限整条忽略，不记入历史、不提示、不复制。仅作用于短信，空格和换行也计入。'/>
                      </StackPanel>
                    </Grid>
                    <Grid>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='150'/>
                        <ColumnDefinition Width='*'/>
                      </Grid.ColumnDefinitions>
                      <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                      </Grid.RowDefinitions>

                      <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='过滤关键字'/>
                      <TextBox x:Name='txtFilterKeywords' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}'
                               Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                               AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                      <TextBlock Grid.Row='1' Grid.Column='1' Style='{StaticResource Hint}' TextWrapping='Wrap'
                                 Text='每行一个关键字，短信包含任一关键字时直接忽略；留空表示不启用' Margin='0,5,0,12'/>

                      <TextBlock Grid.Row='2' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='过滤正则'/>
                      <TextBox x:Name='txtFilterRegex' Grid.Row='2' Grid.Column='1' Style='{StaticResource Input}'
                               Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                               AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                      <TextBlock Grid.Row='3' Grid.Column='1' Style='{StaticResource Hint}' TextWrapping='Wrap'
                                 Text='每行一条 .NET 正则表达式，任一匹配时直接忽略；留空表示不启用' Margin='0,5,0,0'/>
                    </Grid>
                  </StackPanel>
               </Border>

               <Border x:Name='cardEmail' Style='{StaticResource Card}'>
                 <StackPanel>
                   <TextBlock Style='{StaticResource CardTitle}' Text='邮箱连接'/>
                   <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,0,0,12'
                              Text='通过 IMAP 接收新邮件中的验证码；设置修改后自动保存，无需重启。'/>
                   <Grid>
                     <Grid.ColumnDefinitions>
                       <ColumnDefinition Width='140'/>
                       <ColumnDefinition Width='*'/>
                     </Grid.ColumnDefinitions>
                     <Grid.RowDefinitions>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                     </Grid.RowDefinitions>

                     <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='启用邮箱接收'/>
                     <CheckBox x:Name='chkEmail' Grid.Row='0' Grid.Column='1' Content='接收邮箱里的验证码邮件' Foreground='#E4000000' FontSize='14' Height='32' Margin='0,8' VerticalAlignment='Top'/>

                     <TextBlock Grid.Row='1' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='邮箱服务'/>
                     <ComboBox x:Name='cmbEmailProvider' Grid.Row='1' Grid.Column='1' Height='32' FontSize='14' Width='300' HorizontalAlignment='Left'
                               Margin='0,8' VerticalAlignment='Top'
                               Style='{StaticResource PrivacyCombo}' ItemContainerStyle='{StaticResource PrivacyItem}'>
                       <ComboBoxItem Content='QQ 邮箱'/>
                       <ComboBoxItem Content='163 邮箱'/>
                       <ComboBoxItem Content='126 邮箱'/>
                       <ComboBoxItem Content='Gmail'/>
                       <ComboBoxItem Content='Outlook'/>
                       <ComboBoxItem Content='自定义'/>
                     </ComboBox>

                     <TextBlock Grid.Row='2' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='服务器地址'/>
                     <StackPanel Grid.Row='2' Grid.Column='1' Margin='0,8'>
                       <TextBox x:Name='txtEmailServer' Style='{StaticResource Input}' Width='300' HorizontalAlignment='Left'/>
                       <TextBlock Style='{StaticResource Hint}' Text='使用 993 端口，通过 SSL 加密连接' Margin='0,5,0,0' TextWrapping='Wrap'/>
                     </StackPanel>

                     <TextBlock Grid.Row='3' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='邮箱账号'/>
                     <TextBox x:Name='txtEmailAccount' Grid.Row='3' Grid.Column='1' Style='{StaticResource Input}' Width='300' HorizontalAlignment='Left' Margin='0,8' VerticalAlignment='Top'/>

                     <TextBlock Grid.Row='4' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='授权码 / 应用密码'/>
                     <StackPanel Grid.Row='4' Grid.Column='1' Margin='0,8'>
                       <StackPanel x:Name='emailAuthHost' Orientation='Horizontal'/>
                       <TextBlock x:Name='emailAuthHint' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,5,0,0'/>
                     </StackPanel>

                   </Grid>
                   <TextBlock x:Name='emailProviderHint' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,14,0,0'/>
                   <Button x:Name='btnEmailAuthHelp' Style='{StaticResource SecondaryBtn}' Content='打开 Google 应用专用密码设置'
                           HorizontalAlignment='Left' Margin='0,10,0,0' Visibility='Collapsed'/>
                    <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,10,0,0'
                               Text='授权码 / 应用密码采用 Windows 加密存储。只处理启用后新到的邮件，不改动已读状态。'/>
                     <Button x:Name='btnEmailGuide' Style='{StaticResource SecondaryBtn}' Content='Gmail 设置教程' HorizontalAlignment='Left' Margin='0,12,0,0'/>
                 </StackPanel>
               </Border>

               <Border x:Name='cardEmailFilters' Style='{StaticResource Card}'>
                 <StackPanel>
                    <TextBlock Style='{StaticResource CardTitle}' Text='邮件接收规则'/>
                   <Grid>
                     <Grid.ColumnDefinitions>
                       <ColumnDefinition Width='140'/>
                       <ColumnDefinition Width='*'/>
                     </Grid.ColumnDefinitions>
                     <Grid.RowDefinitions>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                       <RowDefinition Height='Auto'/>
                     </Grid.RowDefinitions>

                     <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='主题关键词'/>
                     <StackPanel Grid.Row='0' Grid.Column='1' Margin='0,8'>
                       <WrapPanel x:Name='emailKeywordTags'/>
                        <WrapPanel Margin='0,4,0,0'>
                         <TextBox x:Name='txtEmailKeywordAdd' Style='{StaticResource Input}' Width='210' ToolTip='输入关键词，按回车添加；支持粘贴多行'/>
                          <Button x:Name='btnEmailKeywordAdd' Style='{StaticResource SecondaryBtn}' Content='添加' Margin='8,0,0,6'/>
                          <Button x:Name='btnEmailKeywordDefault' Style='{StaticResource SecondaryBtn}' Content='恢复默认' Margin='8,0,0,6'/>
                        </WrapPanel>
                       <Expander Header='批量编辑（每行一个关键词）' FontSize='12.5' Margin='0,8,0,0'>
                       <TextBox x:Name='txtEmailKeywords' Style='{StaticResource Input}' Margin='0,6,0,0'
                                 Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                                 AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                        </Expander>
                        <Expander Header='测试主题匹配' FontSize='12.5' Margin='0,8,0,0'>
                          <StackPanel>
                            <TextBox x:Name='txtEmailSubjectPreview' Style='{StaticResource Input}' Margin='0,6,0,0' ToolTip='输入示例邮件主题，只在本机预览规则'/>
                            <TextBlock x:Name='lblEmailPreviewResult' Style='{StaticResource Hint}' Margin='0,5,0,0' Text='输入主题查看是否会被接收'/>
                          </StackPanel>
                        </Expander>
                       <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Text='主题包含任一标签才读取正文；留空不接收邮件。' Margin='0,6,0,0'/>
                     </StackPanel>

                     <TextBlock Grid.Row='1' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='发件人白名单'/>
                     <StackPanel Grid.Row='1' Grid.Column='1' Margin='0,8'>
                       <TextBox x:Name='txtEmailSenders' Style='{StaticResource Input}'
                                Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                                AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                        <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Text='每行填写完整邮箱或完整域名，例如 noreply@example.com / example.com；精确匹配，留空不限发件人。From 地址不等于已认证身份。' Margin='0,5,0,0'/>
                     </StackPanel>

                     <TextBlock Grid.Row='2' Grid.Column='0' Style='{StaticResource EmailFieldLabel}' Text='接收选项'/>
                     <WrapPanel x:Name='emailReceiveOptions' Grid.Row='2' Grid.Column='1' Margin='0,8'>
                       <CheckBox x:Name='chkEmailNotify' Content='到达提示' Foreground='#E4000000' FontSize='14' Height='32' Margin='0,0,20,0'/>
                       <CheckBox x:Name='chkEmailAutoCopy' Content='自动复制' Foreground='#E4000000' FontSize='14' Height='32'/>
                     </WrapPanel>
                    </Grid>
                 </StackPanel>
               </Border>

               <Border x:Name='cardQqPopup' Style='{StaticResource Card}'>
                 <StackPanel>
                     <TextBlock Style='{StaticResource CardTitle}' Text='邮箱高级设置'/>
                     <Grid>
                       <Grid.ColumnDefinitions><ColumnDefinition Width='140'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                       <TextBlock Style='{StaticResource EmailFieldLabel}' Text='检查间隔'/>
                       <StackPanel Grid.Column='1' Margin='0,8'>
                         <StackPanel Orientation='Horizontal'>
                           <TextBox x:Name='txtEmailPoll' Style='{StaticResource Input}' Width='96'/>
                           <TextBlock Style='{StaticResource Hint}' Text='秒（10～600）' Margin='10,0,0,0'/>
                         </StackPanel>
                         <TextBlock Style='{StaticResource Hint}' Text='收件箱支持 IDLE 时实时接收；分类目录按间隔检查。排除已发送、草稿和已删除。' TextWrapping='Wrap' Margin='0,5,0,0'/>
                       </StackPanel>
                     </Grid>
                     <CheckBox x:Name='chkEmailJunk' Content='同时检查垃圾 / 广告文件夹' FontSize='14' Margin='0,12,0,0'/>
                     <CheckBox x:Name='chkEmailSaveBody' Content='在记录中保存邮件内容（正文最多 20000 字符）' FontSize='14' Margin='0,12,0,0'/>
                     <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Text='默认仅保存发件人和验证码；历史采用当前 Windows 用户加密，换机或换用户不可直接读取。旧版明文记录下次成功保存时迁移。' Margin='0,6,0,0'/>
                     <TextBlock Style='{StaticResource CardTitle}' Text='QQ 弹窗联动（可选）' Margin='0,18,0,10'/>
                     <CheckBox x:Name='chkQqPopup' Content='QQ 新邮件提醒出现时，立即检查已配置的邮箱' Foreground='#E4000000' FontSize='14'/>
                    <TextBlock x:Name='lblQqPopupRisk' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,6,0,0'
                              Text='需要先配置并启用 IMAP；仅以 QQ 邮件提醒窗口标题唤醒检查，不读取聊天或弹窗正文。'/>
                    <TextBlock x:Name='lblQqStatus' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,6,0,0'/>
                 </StackPanel>
               </Border>

                <Border x:Name='cardNotify' Style='{StaticResource Card}' Visibility='Collapsed'>
                  <StackPanel>
                    <TextBlock Style='{StaticResource CardTitle}' Text='通知隐私'/>
                    <Grid>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='150'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <TextBlock Grid.Row='0' Style='{StaticResource FieldLabel}' Text='短信通知内容'/>
                      <ComboBox x:Name='cmbPrivacy' Grid.Row='0' Grid.Column='1' Height='32' FontSize='14' MaxWidth='340' HorizontalAlignment='Stretch' Margin='0,0,0,12'
                                Style='{StaticResource PrivacyCombo}' ItemContainerStyle='{StaticResource PrivacyItem}'>
                        <ComboBoxItem Content='只提示收到，不显示内容'/>
                        <ComboBoxItem Content='显示内容，验证码替换为星号'/>
                        <ComboBoxItem Content='完整显示短信内容'/>
                      </ComboBox>
                      <TextBlock Grid.Row='1' Style='{StaticResource FieldLabel}' Text='邮件通知内容'/>
                      <ComboBox x:Name='cmbEmailPrivacy' Grid.Row='1' Grid.Column='1' Height='32' FontSize='14' MaxWidth='340' HorizontalAlignment='Stretch'
                                Style='{StaticResource PrivacyCombo}' ItemContainerStyle='{StaticResource PrivacyItem}'>
                        <ComboBoxItem Content='只提示收到，不显示内容'/>
                        <ComboBoxItem Content='显示内容，验证码替换为星号'/>
                        <ComboBoxItem Content='完整显示邮件内容'/>
                      </ComboBox>
                    </Grid>
                    <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,12,0,0' Text='两种通道分别设置，默认只提示收到。此处控制桌面通知，不影响剪贴板或记录原文；记录页可用下方密码锁保护。'/>
                  </StackPanel>
                </Border>

              <Border x:Name='cardSecurity' Style='{StaticResource Card}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource CardTitle}' Text='程序密码锁'/>
                  <CheckBox x:Name='chkAutoLock' Content='启用自动锁定（启动、重开窗口、最小化时需解锁）'
                            Foreground='#E4000000' FontSize='14'/>
                   <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,10,0,0'
                              Text='密码锁保护界面；历史由当前 Windows 用户加密，与密码锁独立，配置和日志仍需妥善保护。自动锁定默认关闭，也可手动立即锁定。'/>
                   <Button x:Name='btnSecurity' Style='{StaticResource SecondaryBtn}' Content='管理密码锁' HorizontalAlignment='Left' Margin='0,14,0,0'/>
                   <Button x:Name='btnLock' Style='{StaticResource SecondaryBtn}' Content='立即锁定' HorizontalAlignment='Left' Margin='0,14,0,0'/>
                 </StackPanel>
               </Border>

              <Border x:Name='cardMore' Style='{StaticResource Card}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource CardTitle}' Text='配置管理'/>
                   <WrapPanel>
                     <Button x:Name='btnGen' Style='{StaticResource SecondaryBtn}' Content='复制手机端配置'/>
                     <Button x:Name='btnImport' Style='{StaticResource SecondaryBtn}' Content='导入设置' Margin='10,0,0,6'/>
                     <Button x:Name='btnExport' Style='{StaticResource SecondaryBtn}' Content='导出设置' Margin='10,0,0,6'/>
                   </WrapPanel>
                   <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Text='设置修改后自动保存；端口 / ntfy 修改后请重启。导出设置用于迁移，排障请使用“关于”中的脱敏诊断。' Margin='0,10,0,0'/>
                </StackPanel>
              </Border>

             </StackPanel>
          </ScrollViewer>
        </Grid>

        <!-- 记录页 -->
        <Grid x:Name='pageLog' Grid.Column='1'>
          <Grid.RowDefinitions>
            <RowDefinition Height='*'/>
            <RowDefinition Height='Auto'/>
          </Grid.RowDefinitions>

          <Border Grid.Row='0' Style='{StaticResource RecordCard}'>
            <Grid>
               <Grid.RowDefinitions>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='*'/>
              </Grid.RowDefinitions>

              <TextBlock Grid.Row='0' Style='{StaticResource CardTitle}' Text='验证码记录'/>

              <WrapPanel Grid.Row='1'>
                <Button x:Name='tabAllHistory' Style='{StaticResource HistoryTab}' Content='全部 (0)'/>
                <Button x:Name='tabSmsHistory' Style='{StaticResource HistoryTab}' Content='短信 (0)'/>
                <Button x:Name='tabEmailHistory' Style='{StaticResource HistoryTab}' Content='邮件 (0)'/>
              </WrapPanel>
              <StackPanel Grid.Row='2' Margin='0,8,0,10'>
              <TextBlock Style='{StaticResource Hint}' Text='搜索服务名、发件人、验证码或已保存原文；空格分隔多个词' TextWrapping='Wrap' Margin='0,0,0,6'/>
              <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                <TextBox x:Name='txtHistorySearch' Style='{StaticResource Input}' ToolTip='搜索服务名、发件人、验证码或已保存原文；多个词以空格分隔' AutomationProperties.Name='搜索验证码记录' MaxLength='256'/>
                <Button x:Name='btnHistoryReset' Grid.Column='1' Style='{StaticResource SecondaryBtn}' Content='重置筛选' Margin='8,0,0,0'/>
              </Grid>
              </StackPanel>

              <Grid Grid.Row='3' Height='30'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='128'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='100'/>
                  <ColumnDefinition Width='2*'/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column='0' Style='{StaticResource TableHeader}' Text='时间'/>
                 <TextBlock Grid.Column='1' Style='{StaticResource TableHeader}' Text='发件人'/>
                <TextBlock Grid.Column='2' Style='{StaticResource TableHeader}' Text='验证码'/>
                <TextBlock Grid.Column='3' Style='{StaticResource TableHeader}' Text='原文'/>
              </Grid>

              <Border Grid.Row='4' BorderBrush='#0F000000' BorderThickness='0,1,0,0'>
                <Grid>
                  <ScrollViewer x:Name='historyScroll' VerticalScrollBarVisibility='Auto' Margin='0,4,0,0'>
                    <StackPanel x:Name='listHost'/>
                  </ScrollViewer>
                  <TextBlock x:Name='lblHistoryEmpty' Style='{StaticResource Hint}' HorizontalAlignment='Center'
                             VerticalAlignment='Center' Text='暂无短信验证码记录' IsHitTestVisible='False' Visibility='Collapsed'/>
                </Grid>
              </Border>
            </Grid>
          </Border>

          <Border Grid.Row='1' Background='Transparent' BorderBrush='#0F000000' BorderThickness='0,1,0,0' Padding='22,12'>
            <StackPanel>
              <WrapPanel Margin='0,0,0,10'>
                <TextBlock Text='自动清除超过' Style='{StaticResource Hint}' Margin='0,0,8,0'/>
                <TextBox x:Name='txtHistoryRetention' Style='{StaticResource Input}' Width='72' MaxLength='5' AutomationProperties.Name='记录自动清除分钟数'/>
                <TextBlock Text='分钟前收到的记录' Style='{StaticResource Hint}' Margin='8,0,0,0'/>
              </WrapPanel>
              <TextBlock Text='短信与邮件记录一起清理；默认 10 分钟，0 停用。不删除邮箱里的邮件。' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,0,0,10'/>
            <WrapPanel>
                <Button x:Name='btnClear' Style='{StaticResource SecondaryBtn}' Content='清空全部记录'/>
              <TextBlock Style='{StaticResource Hint}' Text='单击验证码即可复制' Margin='14,0,0,0'/>
              <TextBlock x:Name='lblCount' Style='{StaticResource Hint}' Margin='14,0,0,0' Text='共 0 条'/>
            </WrapPanel>
            </StackPanel>
          </Border>
        </Grid>

          <Grid x:Name='pageAbout' Grid.Column='1' Visibility='Collapsed'>
            <ScrollViewer VerticalScrollBarVisibility='Auto'>
              <StackPanel Margin='22,20,22,22'>
                <Border Style='{StaticResource Card}'>
                  <StackPanel>
                    <TextBlock x:Name='lblAboutVersion' Foreground='#E4000000' FontSize='14' Text='codepass'/>
                     <WrapPanel Margin='0,16,0,0'>
                      <Button x:Name='btnGithub' Style='{StaticResource SecondaryBtn}' Content='打开 GitHub 主页'/>
                      <Button x:Name='btnUpdate' Style='{StaticResource SecondaryBtn}' Content='检查更新' Margin='10,0,0,0'/>
                     </WrapPanel>
                    <TextBlock x:Name='lblUpdateStatus' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,10,0,0'
                               Text='尚未检查更新'/>
                  </StackPanel>
                </Border>
                 <Border Style='{StaticResource Card}'>
                   <StackPanel>
                     <TextBlock Style='{StaticResource CardTitle}' Text='排障与诊断'/>
                     <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Text='预览并导出脱敏报告：版本、功能开关、记录数量与归类状态。报告不含验证码、正文、账号、地址、令牌或原始日志。'/>
                     <Button x:Name='btnDiagnostics' Style='{StaticResource SecondaryBtn}' Content='预览脱敏诊断' HorizontalAlignment='Left' Margin='0,14,0,0'/>
                   </StackPanel>
                 </Border>
               </StackPanel>
             </ScrollViewer>
           </Grid>

        </Grid>

          <Border x:Name='lockOverlay' Grid.Column='0' Grid.ColumnSpan='2' Grid.Row='0' Grid.RowSpan='2' Background='#F3F3F3' CornerRadius='8' Panel.ZIndex='100' Visibility='Collapsed'>
         <StackPanel Width='360' HorizontalAlignment='Center' VerticalAlignment='Center'>
           <TextBlock Text='程序已锁定' FontSize='20' FontWeight='SemiBold' Foreground='#E4000000' HorizontalAlignment='Center'/>
           <TextBlock Text='请输入密码后继续使用' Style='{StaticResource Hint}' HorizontalAlignment='Center' Margin='0,10,0,16'/>
             <StackPanel Orientation='Horizontal'>
              <PasswordBox x:Name='txtUnlockPassword' Style='{StaticResource PasswordInput}' Width='270'/>
              <TextBox x:Name='txtUnlockPlain' Style='{StaticResource Input}' Width='270' Visibility='Collapsed'/>
              <Button x:Name='btnUnlockEye' Style='{StaticResource SecondaryBtn}' Content='显示' Width='68' Margin='8,0,0,0'/>
            </StackPanel>
           <Button x:Name='btnUnlock' Style='{StaticResource PrimaryBtn}' Content='解锁' Margin='0,14,0,0'/>
           <TextBlock x:Name='lblUnlockError' Foreground='#C42B1C' FontSize='12' TextAlignment='Center' Margin='0,10,0,0'/>
         </StackPanel>
       </Border>

         <!-- 轻提示 (toast): 复制等操作的短反馈, 不拦截鼠标 -->
          <Border x:Name='toastBox' Grid.Column='0' Grid.ColumnSpan='2' Grid.Row='0' Grid.RowSpan='2' Panel.ZIndex='120'
                 HorizontalAlignment='Center' VerticalAlignment='Bottom' Margin='0,0,0,92'
                 Background='#E6253047' CornerRadius='14' Padding='18,9' Visibility='Collapsed'
                 IsHitTestVisible='False'>
           <TextBlock x:Name='toastText' Foreground='#FFFFFF' FontSize='13'/>
         </Border>
     </Grid>
  </Border>
</Grid>";

        // ------------------------------------------------------------------

        public MainWindow(Config cfg, string cfgPath)
        {
            this.cfg = cfg;
            this.cfgPath = cfgPath;

             Title = "codepass";
            WindowStyle = WindowStyle.None;
             AllowsTransparency = true;
                ResizeMode = ResizeMode.CanResize;
             Background = Brushes.Transparent;
              MinWidth = 780;
             MinHeight = 560;
             ApplyConfiguredWindowSize();
             WindowChrome chrome = new WindowChrome();
             chrome.CaptionHeight = 0;
             chrome.ResizeBorderThickness = new Thickness(10);
             chrome.CornerRadius = new CornerRadius(8);
             chrome.GlassFrameThickness = new Thickness(0);
             WindowChrome.SetWindowChrome(this, chrome);
             SizeChanged += delegate { ScheduleWindowSizeSave(); };
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            FontSize = 13.5;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
             System.Windows.Media.Imaging.BitmapSource iconSource = null;
             System.Drawing.Icon ico = null;
             try
             {
                  ico = System.Drawing.Icon.ExtractAssociatedIcon(
                      System.Reflection.Assembly.GetExecutingAssembly().Location);
                  if (ico == null) ico = (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
                 iconSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                     ico.Handle, Int32Rect.Empty,
                     System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                 iconSource.Freeze();
                 Icon = iconSource;
             }
             catch
             {
                 try
                 {
                     if (ico != null) ico.Dispose();
                     ico = (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
                     iconSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                         ico.Handle, Int32Rect.Empty,
                         System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                     iconSource.Freeze();
                     Icon = iconSource;
                 }
                 catch { }
             }
             finally
             {
                 if (ico != null) ico.Dispose();
             }

             Grid root = (Grid)XamlReader.Parse(UiXaml);
             Content = root;
             Image appLogo = Find<Image>(root, "imgAppLogo");
             if (appLogo != null && iconSource != null) appLogo.Source = iconSource;

             txtPort       = Find<TextBox>(root, "txtPort");
             txtHistorySearch = Find<TextBox>(root, "txtHistorySearch");
             txtHistoryRetention = Find<TextBox>(root, "txtHistoryRetention");
             tabAllHistory = Find<Button>(root, "tabAllHistory");
             cardEmailStatus = Find<Border>(root, "cardEmailStatus");
             cardNtfy = Find<Border>(root, "cardNtfy");
             cardSmsFilters = Find<Border>(root, "cardSmsFilters");
             txtPcIp       = Find<TextBox>(root, "txtPcIp");
              txtNtfyServer = Find<TextBox>(root, "txtNtfyServer");
             txtNtfyTopic  = Find<TextBox>(root, "txtNtfyTopic");
            txtMin        = Find<TextBox>(root, "txtMin");
            txtSmsMaxLength = Find<TextBox>(root, "txtSmsMaxLength");
              txtMax        = Find<TextBox>(root, "txtMax");
              txtClear      = Find<TextBox>(root, "txtClear");
              txtFilterKeywords = Find<TextBox>(root, "txtFilterKeywords");
              txtFilterRegex = Find<TextBox>(root, "txtFilterRegex");
              chkTip        = Find<CheckBox>(root, "chkTip");
              chkStartup    = Find<CheckBox>(root, "chkStartup");
              chkAutoLock   = Find<CheckBox>(root, "chkAutoLock");
              cmbPrivacy    = Find<ComboBox>(root, "cmbPrivacy");
              cmbEmailPrivacy = Find<ComboBox>(root, "cmbEmailPrivacy");
             btnTestPc     = Find<Button>(root, "btnTestPc");
             btnTestNtfy   = Find<Button>(root, "btnTestNtfy");
             btnLock       = Find<Button>(root, "btnLock");
             listHost      = Find<StackPanel>(root, "listHost");
             lblCount      = Find<TextBlock>(root, "lblCount");
             lblHistoryEmpty = Find<TextBlock>(root, "lblHistoryEmpty");
             tabSmsHistory = Find<Button>(root, "tabSmsHistory");
             tabEmailHistory = Find<Button>(root, "tabEmailHistory");
             historyScroll = Find<ScrollViewer>(root, "historyScroll");
            navHistory   = Find<Button>(root, "navHistory");
             navGeneral   = Find<Button>(root, "navGeneral");
             navEmail     = Find<Button>(root, "navEmail");
            navSecurity  = Find<Button>(root, "navSecurity");
            navAbout     = Find<Button>(root, "navAbout");
            pageSet       = Find<Grid>(root, "pageSet");
            pageLog       = Find<Grid>(root, "pageLog");
            pageAbout     = Find<Grid>(root, "pageAbout");
            settingsScroll = Find<ScrollViewer>(root, "settingsScroll");
            cardGeneral   = Find<Border>(root, "cardGeneral");
            cardNetwork   = Find<Border>(root, "cardNetwork");
            cardNotify    = Find<Border>(root, "cardNotify");
            cardSecurity  = Find<Border>(root, "cardSecurity");
            cardMore      = Find<Border>(root, "cardMore");
              cardEmail     = Find<Border>(root, "cardEmail");
              cardEmailFilters = Find<Border>(root, "cardEmailFilters");
              cardQqPopup = Find<Border>(root, "cardQqPopup");
              emailAuthHint = Find<TextBlock>(root, "emailAuthHint");
              emailProviderHint = Find<TextBlock>(root, "emailProviderHint");
               btnEmailAuthHelp = Find<Button>(root, "btnEmailAuthHelp");
               txtEmailKeywordAdd = Find<TextBox>(root, "txtEmailKeywordAdd");
               emailKeywordTags = Find<WrapPanel>(root, "emailKeywordTags");
             txtEmailServer = Find<TextBox>(root, "txtEmailServer");
             txtEmailAccount = Find<TextBox>(root, "txtEmailAccount");
             txtEmailPoll = Find<TextBox>(root, "txtEmailPoll");
             txtEmailKeywords = Find<TextBox>(root, "txtEmailKeywords");
             txtEmailSenders = Find<TextBox>(root, "txtEmailSenders");
             chkEmail = Find<CheckBox>(root, "chkEmail");
             chkEmailJunk = Find<CheckBox>(root, "chkEmailJunk");
             chkEmailNotify = Find<CheckBox>(root, "chkEmailNotify");
              chkEmailAutoCopy = Find<CheckBox>(root, "chkEmailAutoCopy");
              chkEmailSaveBody = Find<CheckBox>(root, "chkEmailSaveBody");
              lblEmailStatus = Find<TextBlock>(root, "lblEmailStatus");
              btnEmailTest = Find<Button>(root, "btnEmailTest");
              txtEmailSubjectPreview = Find<TextBox>(root, "txtEmailSubjectPreview");
              lblEmailPreviewResult = Find<TextBlock>(root, "lblEmailPreviewResult");
             chkQqPopup = Find<CheckBox>(root, "chkQqPopup");
             cmbEmailProvider = Find<ComboBox>(root, "cmbEmailProvider");
             dragBar       = Find<Border>(root, "dragBar");
             lockOverlay   = Find<Border>(root, "lockOverlay");
             txtUnlockPassword = Find<PasswordBox>(root, "txtUnlockPassword");
             txtUnlockPlain = Find<TextBox>(root, "txtUnlockPlain");
             btnUnlockEye = Find<Button>(root, "btnUnlockEye");
              if (txtUnlockPassword != null && txtUnlockPlain != null && btnUnlockEye != null)
                  unlockRevealer = new PasswordRevealer(txtUnlockPassword, txtUnlockPlain, btnUnlockEye);
              lblUnlockError = Find<TextBlock>(root, "lblUnlockError");
              toastBox = Find<Border>(root, "toastBox");
              toastText = Find<TextBlock>(root, "toastText");
              btnGithub = Find<Button>(root, "btnGithub");
              btnUpdate = Find<Button>(root, "btnUpdate");
              lblAboutVersion = Find<TextBlock>(root, "lblAboutVersion");
              lblUpdateStatus = Find<TextBlock>(root, "lblUpdateStatus");
              if (lblAboutVersion != null)
                   lblAboutVersion.Text = "codepass v" + Updater.AppVersion;
              lblEmailFolders = Find<TextBlock>(root, "lblEmailFolders");
              lblEmailProcessing = Find<TextBlock>(root, "lblEmailProcessing");
              lblEmailTestResult = Find<TextBlock>(root, "lblEmailTestResult");
              lblStorageError = Find<TextBlock>(root, "lblStorageError");
              lblQqStatus = Find<TextBlock>(root, "lblQqStatus");
              BuildSecretFields(root);
              InitializeResponsiveForms(pageSet);
              settingsScroll.SizeChanged += delegate { UpdateResponsiveForms(); };
              Loaded += delegate { UpdateResponsiveForms(); };
              TextBlock qqRisk = Find<TextBlock>(root, "lblQqPopupRisk");
              if (qqRisk != null) qqRisk.Text = "仅以邮件提醒窗口标题唤醒 IMAP；不读取弹窗或聊天正文，不直接提取验证码。需要先配置邮箱，最终仍执行主题、发件人与时效规则。";
             if (btnLock != null) btnLock.Click += delegate { LockWindow(); };

            // 顶栏拖动 + 窗口按钮
            if (dragBar != null)
                dragBar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    if (e.ButtonState == MouseButtonState.Pressed)
                    {
                        try { DragMove(); } catch { }
                    }
                };
            Wire(root, "btnMin", delegate { WindowState = WindowState.Minimized; });
            Wire(root, "btnClose", delegate { Close(); });

             Wire(root, "btnDetect", delegate(object sender, RoutedEventArgs e) { DetectLocalIp(sender as Button); });
             Wire(root, "btnTestPc", delegate { TestPcConnectivity(); });
             Wire(root, "btnTestNtfy", delegate { TestNtfyConnectivity(); });
             Wire(root, "btnGen", delegate { OnGeneratePhoneConfig(); });
             Wire(root, "btnImport", delegate { OnImport(); });
             Wire(root, "btnExport", delegate { OnExport(); });
             Wire(root, "btnSecurity", delegate { ConfigureSecurity(); });
             Wire(root, "btnUnlock", delegate { TryUnlock(); });
              Wire(root, "btnGithub", delegate { OpenRepoPage(); });
               Wire(root, "btnUpdate", delegate { CheckUpdate(); });
               Wire(root, "btnEmailAuthHelp", delegate
               {
                   try { Process.Start(new ProcessStartInfo("https://myaccount.google.com/apppasswords") { UseShellExecute = true }); }
                   catch { ShowToast("无法打开浏览器，请在 Google 账号设置中查找应用专用密码"); }
               });
               Wire(root, "tabSmsHistory", delegate { SelectHistoryChannel("sms"); });
               Wire(root, "tabEmailHistory", delegate { SelectHistoryChannel("email"); });
               Wire(root, "tabAllHistory", delegate { SelectHistoryChannel("all"); });
               Wire(root, "btnHistoryReset", delegate { txtHistorySearch.Clear(); SelectHistoryChannel("all"); });
               txtHistorySearch.TextChanged += delegate { RefreshHistory(); if (historyScroll != null) historyScroll.ScrollToTop(); };
               txtHistorySearch.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
               { if (e.Key == Key.Escape) { txtHistorySearch.Clear(); e.Handled = true; } };
               Wire(root, "btnDiagnostics", delegate { ShowDiagnostics(); });
                Wire(root, "btnEmailKeywordAdd", delegate { AddEmailKeywords(); });
                Wire(root, "btnEmailKeywordDefault", delegate
                {
                    if (MessageBox.Show(this, "恢复默认主题关键词？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                    txtEmailKeywords.Text = new Config().EmailKeywords; ApplySettings();
                });
                 Wire(root, "btnEmailTest", delegate { TestEmailConnection(); });
                 Wire(root, "btnStorageRetry", delegate
                 {
                     if (!ApplySettings()) return;
                     try { cfg.Save(cfgPath); History.RetrySave(); EmailWatcher.Kick(); ShowToast("已重试保存并请求邮件检查"); }
                     catch { ShowToast("保存仍未完成，请检查文件权限与磁盘空间"); }
                     UpdateEmailStatus();
                 });
                Wire(root, "btnEmailRecheck", delegate
                {
                    if (!ApplySettings()) return;
                    if (!Program.EmailConfigured()) { ShowToast("请先填写邮箱凭据并开启邮箱接收"); return; }
                    EmailWatcher.RequestRecentCheck();
                    ShowToast("已请求复查近 30 分钟邮件，最多 200 封候选邮件");
                });
                Wire(root, "btnEmailGuide", delegate
                {
                    string guide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GMAIL_APP_PASSWORD.md");
                    if (!File.Exists(guide)) guide = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "docs", "GMAIL_APP_PASSWORD.md"));
                    try { Process.Start(new ProcessStartInfo(File.Exists(guide) ? guide : "https://support.google.com/accounts/answer/185833?hl=zh-Hans") { UseShellExecute = true }); }
                    catch { ShowToast("请查看交付目录的 GMAIL_APP_PASSWORD.md 教程"); }
                });
            Wire(root, "btnClear", delegate
            {
                 if (MessageBox.Show(this, "确定清空全部记录？", "确认",
                        MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                {
                    try { History.Clear(); RefreshHistory(); }
                    catch (IOException) { ShowToast(History.LastSaveError); }
                }
            });

              if (navHistory != null) navHistory.Click += delegate { SelectSection("history"); };
               if (navGeneral != null) navGeneral.Click += delegate { SelectSection("general"); };
               if (navEmail != null) navEmail.Click += delegate { SelectSection("email"); };
              if (navSecurity != null) navSecurity.Click += delegate { SelectSection("security"); };
              if (navAbout != null) navAbout.Click += delegate { SelectSection("about"); };
             StateChanged += delegate
             {
                 if (WindowState == WindowState.Minimized) wasMinimized = true;
                 else if (wasMinimized)
                 {
                     wasMinimized = false;
                     if (cfg.ShouldAutoLock) LockWindow();
                 }
             };

              LoadValues();
              RefreshHistory();
              WireAutoApply(root);
              txtHistoryRetention.LostFocus += delegate { SaveHistoryRetention(); };
              txtHistoryRetention.KeyDown += delegate(object sender, KeyEventArgs e)
              { if (e.Key == Key.Return) { SaveHistoryRetention(); e.Handled = true; } };
               WireMultilineScrolling();
              SelectSection("history");
             if (cfg.LockOnOpen) LockWindow();
#if CODEPASS_TEST
             // 测试构建专用：设置 CODEPASS_UPDATE_AUTO=1 时启动即自动检查更新（便于本地联调）
             if (Environment.GetEnvironmentVariable("CODEPASS_UPDATE_AUTO") == "1")
                 Loaded += delegate { CheckUpdate(); };
#endif
        }

         /// <summary>输入完成即应用：文本失焦/回车、勾选切换、下拉选择、密钥框失焦时自动保存。</summary>
         void WireAutoApply(FrameworkElement root)
        {
               TextBox[] boxes = new TextBox[] { txtPort, txtPcIp, txtMin, txtMax, txtClear, txtSmsMaxLength,
                   txtNtfyServer, txtNtfyTopic, txtFilterKeywords, txtFilterRegex,
                   txtEmailServer, txtEmailAccount, txtEmailPoll, txtEmailKeywords, txtEmailSenders };
            foreach (TextBox tb in boxes)
            {
                if (tb == null) continue;
                tb.LostFocus += delegate { ApplySettings(); };
                 tb.KeyDown += delegate(object s, KeyEventArgs e)
                 {
                     // 多行过滤/邮箱输入框用回车换行，失焦时自动保存；其他输入框回车立即保存。
                     if (e.Key == Key.Return
                         && s != txtFilterKeywords && s != txtFilterRegex
                         && s != txtEmailKeywords && s != txtEmailSenders) ApplySettings();
                 };
            }
            if (chkTip != null) chkTip.Click += delegate { ApplySettings(); };
            if (chkStartup != null) chkStartup.Click += delegate { ApplySettings(); };
            if (chkAutoLock != null) chkAutoLock.Click += delegate { ApplySettings(); };
              if (cmbPrivacy != null) cmbPrivacy.SelectionChanged += delegate { ApplySettings(); };
              if (cmbEmailPrivacy != null) cmbEmailPrivacy.SelectionChanged += delegate { ApplySettings(); };
              if (fldToken != null) fldToken.Committed += delegate { ApplySettings(); };
              if (fldNtfyToken != null) fldNtfyToken.Committed += delegate { ApplySettings(); };
              if (fldEmailAuth != null) fldEmailAuth.Committed += delegate { ApplySettings(); };
              if (chkEmail != null) chkEmail.Click += delegate { ApplySettings(); };
              if (chkEmailJunk != null) chkEmailJunk.Click += delegate { ApplySettings(); };
              if (chkEmailNotify != null) chkEmailNotify.Click += delegate { ApplySettings(); };
               if (chkEmailAutoCopy != null) chkEmailAutoCopy.Click += delegate { ApplySettings(); };
               if (chkEmailSaveBody != null) chkEmailSaveBody.Click += delegate { ApplySettings(); };
              if (chkQqPopup != null) chkQqPopup.Click += delegate { ApplySettings(); };
              if (cmbEmailProvider != null) cmbEmailProvider.SelectionChanged += delegate
              {
                  if (loadingValues) return;
                  ApplyEmailProviderPreset();
                  ApplySettings();
              };
              if (txtPcIp != null) txtPcIp.TextChanged += delegate { if (!loadingValues) autoPcIp = ""; };
               if (txtEmailServer != null) txtEmailServer.TextChanged += delegate { UpdateEmailAuthHint(); };
               if (txtEmailKeywords != null) txtEmailKeywords.TextChanged += delegate { RenderEmailKeywordTags(); };
               if (txtEmailSubjectPreview != null) txtEmailSubjectPreview.TextChanged += delegate { UpdateEmailPreview(); };
               if (txtEmailKeywordAdd != null) txtEmailKeywordAdd.KeyDown += delegate(object s, KeyEventArgs e)
               {
                   if (e.Key == Key.Return) { AddEmailKeywords(); e.Handled = true; }
               };
         }

         void WireMultilineScrolling()
         {
             TextBox[] boxes = { txtFilterKeywords, txtFilterRegex, txtEmailKeywords, txtEmailSenders };
             foreach (TextBox box in boxes)
                 if (box != null) box.PreviewMouseWheel += ScrollMultilineTextBox;
         }

         static void ScrollMultilineTextBox(object sender, MouseWheelEventArgs e)
         {
             TextBox box = sender as TextBox;
             if (box == null || e.Delta == 0) return;
             box.ApplyTemplate();
             ScrollViewer scroll = box.Template.FindName("PART_ContentHost", box) as ScrollViewer;
             if (scroll == null || scroll.ScrollableHeight <= 0.5) return;
             // 溢出时，包括上下边界，都由输入框消费滚轮，避免意外滚动外层设置页。
             int lines = SystemParameters.WheelScrollLines;
             double step = lines < 0 ? scroll.ViewportHeight : lines * box.FontSize * 1.4;
             box.ScrollToVerticalOffset(box.VerticalOffset - e.Delta / 120.0 * step);
             e.Handled = true;
         }

        static void Wire(FrameworkElement root, string name, RoutedEventHandler h)
        {
            Button b = Find<Button>(root, name);
            if (b != null) b.Click += h;
        }

        static T Find<T>(FrameworkElement root, string name) where T : class
        {
            object o = root.FindName(name);
            if (o == null) o = LogicalTreeHelper.FindLogicalNode(root, name);
            return o as T;
        }

        // 保存表单原始位置，窄窗口只改变布局，不重新创建输入框或触发保存。
        sealed class ResponsiveForm
        {
            internal Grid Grid;
            internal GridLength LabelWidth;
            internal readonly List<GridLength> Rows = new List<GridLength>();
            internal readonly List<FormCell> Cells = new List<FormCell>();
            internal bool Compact;
        }

        sealed class FormCell
        {
            internal FrameworkElement Element;
            internal int Row, Column;
            internal Thickness Margin;
        }

        void InitializeResponsiveForms(DependencyObject node)
        {
            Grid grid = node as Grid;
            if (grid != null && grid.ColumnDefinitions.Count == 2
                && grid.ColumnDefinitions[0].Width.IsAbsolute && grid.ColumnDefinitions[1].Width.IsStar)
            {
                ResponsiveForm form = new ResponsiveForm { Grid = grid, LabelWidth = grid.ColumnDefinitions[0].Width };
                foreach (RowDefinition row in grid.RowDefinitions) form.Rows.Add(row.Height);
                foreach (UIElement child in grid.Children)
                {
                    FrameworkElement element = child as FrameworkElement;
                    if (element != null) form.Cells.Add(new FormCell { Element = element, Row = Grid.GetRow(element),
                        Column = Grid.GetColumn(element), Margin = element.Margin });
                }
                responsiveForms.Add(form);
            }
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject) InitializeResponsiveForms((DependencyObject)child);
        }

        void UpdateResponsiveForms()
        {
            if (settingsScroll == null || settingsScroll.ActualWidth <= 0) return;
            bool compact = settingsScroll.ActualWidth < 850;
            foreach (ResponsiveForm form in responsiveForms)
            {
                if (form.Compact == compact) continue;
                form.Compact = compact;
                form.Grid.ColumnDefinitions[0].Width = compact ? new GridLength(0) : form.LabelWidth;
                form.Grid.RowDefinitions.Clear();
                int rows = Math.Max(1, form.Rows.Count);
                for (int i = 0; i < (compact ? rows * 2 : rows); i++)
                    form.Grid.RowDefinitions.Add(new RowDefinition { Height = compact ? GridLength.Auto : form.Rows.Count == 0 ? GridLength.Auto : form.Rows[i] });
                foreach (FormCell cell in form.Cells)
                {
                    Grid.SetColumn(cell.Element, compact ? 1 : cell.Column);
                    Grid.SetRow(cell.Element, compact ? cell.Row * 2 + (cell.Column == 0 ? 0 : 1) : cell.Row);
                    cell.Element.Margin = compact ? cell.Column == 0 ? new Thickness(0, 10, 0, 4) : new Thickness(0, 0, 0, 8) : cell.Margin;
                }
            }
        }

        Window CreateDiagnosticsWindow()
        {
            string report = Diagnostics.CreateReport(cfg);
            Window dialog = new Window { Title = "脱敏诊断预览", Width = 760, Height = 600, MinWidth = 480, MinHeight = 360,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
                Background = MakeBrush(0xF3, 0xF3, 0xF3), FontFamily = FontFamily, Icon = Icon };
            Grid layout = new Grid { Margin = new Thickness(20) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(new TextBlock { Text = "报告只包含下方预览内容。确认后保存为文本，再自行分享；不会自动上传。",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), Foreground = FgBody });
            TextBox preview = new TextBox { Name = "txtDiagnosticsPreview", Text = report, IsReadOnly = true,
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12), Background = Brushes.White, Foreground = FgBody };
            Grid.SetRow(preview, 1); layout.Children.Add(preview);
            WrapPanel actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Button save = new Button { Name = "btnDiagnosticsSave", Content = "保存此预览", MinWidth = 110 };
            Button close = new Button { Content = "关闭", MinWidth = 84, Margin = new Thickness(8, 0, 0, 0) };
            FrameworkElement root = Content as FrameworkElement;
            if (root != null) { save.Style = root.Resources["PrimaryBtn"] as Style; close.Style = root.Resources["SecondaryBtn"] as Style; }
            save.Click += delegate
            {
                SaveFileDialog picker = new SaveFileDialog { Filter = "文本报告 (*.txt)|*.txt", DefaultExt = ".txt",
                    FileName = "codepass-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" };
                if (picker.ShowDialog(dialog) != true) return;
                try { Diagnostics.SavePreview(picker.FileName, report, cfgPath); ShowToast("脱敏诊断已保存"); }
                catch { MessageBox.Show(dialog, "报告保存失败，请选择其他文件，检查目录权限与磁盘空间。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            close.Click += delegate { dialog.Close(); };
            actions.Children.Add(save); actions.Children.Add(close);
            Grid.SetRow(actions, 2); layout.Children.Add(actions);
            dialog.Content = layout;
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { dialog.Close(); e.Handled = true; } };
            diagnosticsWindow = dialog;
            dialog.Closed += delegate { if (diagnosticsWindow == dialog) diagnosticsWindow = null; };
            dialog.StateChanged += delegate
            {
                if (dialog.WindowState == WindowState.Minimized && cfg.ShouldAutoLock) LockWindow();
            };
            return dialog;
        }

        void ShowDiagnostics()
        {
            if (lockOverlay != null && lockOverlay.Visibility == Visibility.Visible) return;
            CreateDiagnosticsWindow().ShowDialog();
        }

          // ---------- 左侧分类 ----------

         void SelectSection(string section)
         {
             if (pageSet == null) return;
             bool history = section == "history";
             bool about = section == "about";
             pageLog.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
             pageSet.Visibility = (!history && !about) ? Visibility.Visible : Visibility.Collapsed;
              if (pageAbout != null) pageAbout.Visibility = about ? Visibility.Visible : Visibility.Collapsed;
              bool general = !history && !about && (section == "general" || section == "network" || section == "notify");
               bool security = !history && !about && section == "security";
               bool email = !history && !about && section == "email";
              SetCardState(cardGeneral, general);
              SetCardState(cardNetwork, general);
              SetCardState(cardNtfy, general);
              SetCardState(cardSmsFilters, general);
              SetCardState(cardEmailStatus, email);
               SetCardState(cardEmail, email);
               SetCardState(cardEmailFilters, email);
               SetCardState(cardQqPopup, email);
              SetCardState(cardNotify, security);
              SetCardState(cardSecurity, security);
              SetCardState(cardMore, general);
              SetNavState(navHistory, history);
               SetNavState(navGeneral, general);
               SetNavState(navEmail, email);
              SetNavState(navSecurity, security);
              SetNavState(navAbout, about);
               if (general || email || security)
               {
                   if (settingsScroll != null) settingsScroll.ScrollToTop();
                    Border target = security ? cardNotify : email ? cardEmailStatus : cardGeneral;
                  if (target != null) target.BringIntoView();
              }
         }

          static void SetNavState(Button button, bool selected)
         {
              if (button != null) button.Tag = selected ? "On" : null;
          }

          static void SetCardState(Border card, bool visible)
          {
              if (card != null) card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
          }

        // ---------- 数据 <-> 界面 ----------

         void LoadValues()
         {
             if (txtPort == null) return;
             loadingValues = true;
             try
             {
                  txtPort.Text = cfg.Port.ToString();
                  txtHistoryRetention.Text = cfg.HistoryRetentionMinutes.ToString();
                 fldToken.Text = cfg.Token;
                 autoPcIp = cfg.PcIp.Length == 0 ? Program.DetectLocalIp() : "";
                 if (autoPcIp == "127.0.0.1") autoPcIp = "";
                 txtPcIp.Text = cfg.PcIp.Length > 0 ? cfg.PcIp : autoPcIp;
                  txtNtfyServer.Text = cfg.NtfyServer;
                  txtNtfyTopic.Text = cfg.NtfyTopic;
                  fldNtfyToken.Text = cfg.NtfyToken;
                  txtMin.Text = cfg.MinLen.ToString();
                   txtMax.Text = cfg.MaxLen.ToString();
                   txtSmsMaxLength.Text = cfg.SmsMaxLength.ToString();
                  txtClear.Text = cfg.AutoClearSeconds.ToString();
                  if (txtFilterKeywords != null) txtFilterKeywords.Text = cfg.FilterKeywords;
                  if (txtFilterRegex != null) txtFilterRegex.Text = cfg.FilterRegex;
                  if (txtEmailServer != null)
                  {
                      txtEmailServer.Text = cfg.EmailServer;
                      txtEmailAccount.Text = cfg.EmailAccount;
                       if (fldEmailAuth != null) fldEmailAuth.Text = cfg.EmailAuth;
                      txtEmailPoll.Text = cfg.EmailPollSeconds.ToString();
                      txtEmailKeywords.Text = cfg.EmailKeywords;
                      txtEmailSenders.Text = cfg.EmailSenders;
                  }
                  if (chkEmail != null)
                  {
                      chkEmail.IsChecked = cfg.EmailEnabled;
                      chkEmailJunk.IsChecked = cfg.EmailCheckJunk;
                      chkEmailNotify.IsChecked = cfg.EmailNotify;
                       chkEmailAutoCopy.IsChecked = cfg.EmailAutoCopy;
                       chkEmailSaveBody.IsChecked = cfg.EmailSaveBody;
                      chkQqPopup.IsChecked = cfg.QqPopupEnabled;
                  }
                  if (cmbEmailProvider != null)
                      cmbEmailProvider.SelectedIndex = EmailProviderIndexOf(cfg.EmailServer);
                  if (cfg.EmailAuthUndecryptable && !warnedEmailAuthUndecryptable)
                  {
                      warnedEmailAuthUndecryptable = true;
                      ShowToast("邮箱授权码已失效（换机或换了 Windows 账户），请重新填写");
                  }
                   UpdateEmailAuthHint();
                    RenderEmailKeywordTags();
                    UpdateEmailStatus();
                  chkTip.IsChecked = cfg.ShowTip;
                 if (chkStartup != null) chkStartup.IsChecked = cfg.StartWithWindows;
                  if (cmbPrivacy != null) cmbPrivacy.SelectedIndex = cfg.PrivacyMode < 0 || cfg.PrivacyMode > 2 ? 0 : cfg.PrivacyMode;
                  if (cmbEmailPrivacy != null) cmbEmailPrivacy.SelectedIndex = cfg.EmailPrivacyMode;
                 RefreshSecurityControls();
             }
             finally { loadingValues = false; }
         }

        /// <summary>输入完成即应用（失焦、回车、勾选切换）；无效输入提示并还原为已保存值。</summary>
        bool SaveHistoryRetention()
        {
            int minutes;
            if (!int.TryParse(txtHistoryRetention.Text.Trim(), out minutes) || minutes < 0 || minutes > Config.MaximumHistoryRetentionMinutes)
            {
                txtHistoryRetention.Text = cfg.HistoryRetentionMinutes.ToString();
                ShowToast("记录保留时间需在 0～43200 分钟之间，0 停用，已还原");
                return false;
            }
            lock (Config.SaveGate)
            {
                if (minutes != cfg.HistoryRetentionMinutes)
                {
                    int previous = cfg.HistoryRetentionMinutes;
                    cfg.HistoryRetentionMinutes = minutes;
                    try { cfg.Save(cfgPath); }
                    catch
                    {
                        cfg.HistoryRetentionMinutes = previous;
                        txtHistoryRetention.Text = previous.ToString();
                        ShowToast("记录清理设置保存失败，已还原，请检查文件权限");
                        return false;
                    }
                }
            }
            Program.ApplyHistoryRetention();
            return true;
        }

          bool ApplySettings()
         {
              if (applying || loadingValues || txtPort == null) return !settingsRecoveryRequired;
             applying = true;
             try
            {
            int port, mn, mx, cl, smsMaxLength;
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
            {
                 ShowToast("端口无效（1~65535），已还原");
                 txtPort.Text = cfg.Port.ToString();
                 return false;
            }
             if (!int.TryParse(txtMin.Text.Trim(), out mn) || mn < 1 || mn > 32
                 || !int.TryParse(txtMax.Text.Trim(), out mx) || mx < 1 || mx > 32)
             {
                 ShowToast("验证码长度需在 1～32 之间，已还原");
                  txtMin.Text = cfg.MinLen.ToString();
                  txtMax.Text = cfg.MaxLen.ToString();
                  return false;
             }
              if (mx < mn) { mx = mn; txtMax.Text = mx.ToString(); }
              if (!int.TryParse(txtSmsMaxLength.Text.Trim(), out smsMaxLength)
                  || smsMaxLength < 1 || smsMaxLength > Config.MaximumSmsMaxLength)
              {
                  txtSmsMaxLength.Text = cfg.SmsMaxLength.ToString();
                  ShowToast("短信长度上限需在 1～10000 字符之间，已还原");
                  return false;
              }
              if (!int.TryParse(txtClear.Text.Trim(), out cl) || cl < 0)
              {
                   ShowToast("自动清除需为不小于 0 的秒数，已还原");
                   txtClear.Text = cfg.AutoClearSeconds.ToString();
                   return false;
             }

              int emailPoll;
              if (!int.TryParse(txtEmailPoll.Text.Trim(), out emailPoll) || emailPoll < 10 || emailPoll > 600)
              {
                  ShowToast("邮箱检查间隔需在 10～600 秒之间，已还原");
                  txtEmailPoll.Text = cfg.EmailPollSeconds.ToString();
                  return false;
              }
              string newEmailServer = txtEmailServer == null ? "" : txtEmailServer.Text.Trim();
              string newEmailAccount = txtEmailAccount == null ? "" : txtEmailAccount.Text.Trim();
               string newEmailAuth = fldEmailAuth == null ? "" : fldEmailAuth.Text.Trim();
              string newEmailKeywords = txtEmailKeywords == null ? "" : txtEmailKeywords.Text;
               string newEmailSenders = txtEmailSenders == null ? "" : txtEmailSenders.Text;
               string normalizedKeywords, normalizedSenders, mailRuleError;
               if (!MailRules.NormalizeLines(newEmailKeywords, out normalizedKeywords, out mailRuleError))
               {
                   txtEmailKeywords.Text = cfg.EmailKeywords;
                   ShowToast("主题关键词无效：" + mailRuleError + "，已还原"); return false;
               }
                if (!MailRules.NormalizeSenders(newEmailSenders, out normalizedSenders, out mailRuleError))
               {
                   txtEmailSenders.Text = cfg.EmailSenders;
                   ShowToast("发件人规则无效：" + mailRuleError + "，已还原"); return false;
               }
               newEmailKeywords = normalizedKeywords; newEmailSenders = normalizedSenders;
               txtEmailKeywords.Text = newEmailKeywords; txtEmailSenders.Text = newEmailSenders;
              bool newEmailEnabled = chkEmail != null && chkEmail.IsChecked == true;
              bool newEmailJunk = chkEmailJunk != null && chkEmailJunk.IsChecked == true;
              bool newEmailNotify = chkEmailNotify == null || chkEmailNotify.IsChecked == true;
               bool newEmailAutoCopy = chkEmailAutoCopy == null || chkEmailAutoCopy.IsChecked == true;
               bool newEmailSaveBody = chkEmailSaveBody != null && chkEmailSaveBody.IsChecked == true;
               bool newQqPopup = chkQqPopup == null || chkQqPopup.IsChecked == true;
               if (newEmailEnabled
                   && (newEmailServer.Length == 0 || newEmailAccount.Length == 0
                       || newEmailAuth.Length == 0
                      || newEmailServer.IndexOf(' ') >= 0 || newEmailServer.IndexOf('/') >= 0
                      || newEmailServer.IndexOf(':') >= 0))
              {
                  txtEmailServer.Text = cfg.EmailServer;
                  txtEmailAccount.Text = cfg.EmailAccount;
                  if (fldEmailAuth != null) fldEmailAuth.Text = cfg.EmailAuth;
                  if (chkEmail != null) chkEmail.IsChecked = cfg.EmailEnabled;
                  ShowToast("启用邮箱接收需要填写服务器、账号和授权码，已还原");
                  return false;
              }
              string newToken = fldToken.Text.Trim();
              string newPcIp = txtPcIp.Text.Trim();
              if (cfg.PcIp.Length == 0 && autoPcIp.Length > 0 && newPcIp == autoPcIp) newPcIp = "";
              string newNtfyServer = txtNtfyServer.Text.Trim();
              string newNtfyTopic = txtNtfyTopic.Text.Trim();
              string newNtfyToken = fldNtfyToken.Text.Trim();
              string newFilterKeywords = txtFilterKeywords == null ? "" : txtFilterKeywords.Text;
              string newFilterRegex = txtFilterRegex == null ? "" : txtFilterRegex.Text;
              string filterError;
              if (!CodeExtractor.ValidateFilters(newFilterKeywords, newFilterRegex, out filterError))
              {
                  if (txtFilterKeywords != null) txtFilterKeywords.Text = cfg.FilterKeywords;
                  if (txtFilterRegex != null) txtFilterRegex.Text = cfg.FilterRegex;
                  ShowToast("信息过滤规则无效：" + filterError);
                  return false;
              }
             if (newPcIp.Length > 0)
             {
                  if (!IsUsablePcIp(newPcIp))
                 {
                     string restorePcIp = cfg.PcIp.Length > 0 ? cfg.PcIp : autoPcIp;
                     txtPcIp.Text = restorePcIp;
                     if (cfg.PcIp.Length == 0) autoPcIp = restorePcIp;
                     ShowToast("电脑 IPv4 地址无效，已还原");
                     return false;
                 }
             }
             if (newNtfyServer.Length == 0 && newNtfyTopic.Length > 0)
             {
                 txtNtfyServer.Text = cfg.NtfyServer;
                 ShowToast("启用 ntfy 主题时必须填写服务器地址，已还原");
                 return false;
             }
              if (newNtfyServer.Length > 0)
             {
                 Uri ntfyUri;
                  if (!Uri.TryCreate(newNtfyServer, UriKind.Absolute, out ntfyUri)
                       || ntfyUri.Scheme != Uri.UriSchemeHttps
                       || ntfyUri.Host.Length == 0 || ntfyUri.UserInfo.Length > 0)
                  {
                      txtNtfyServer.Text = cfg.NtfyServer;
                      ShowToast("ntfy 服务器必须使用 HTTPS，已还原");
                     return false;
                 }
              }
             bool newShowTip = chkTip.IsChecked == true;
            bool newStartup = chkStartup != null && chkStartup.IsChecked == true;
             int newPrivacy = cmbPrivacy != null && cmbPrivacy.SelectedIndex >= 0 && cmbPrivacy.SelectedIndex <= 2 ? cmbPrivacy.SelectedIndex : 0;
             int newEmailPrivacy = cmbEmailPrivacy != null && cmbEmailPrivacy.SelectedIndex >= 0 && cmbEmailPrivacy.SelectedIndex <= 2 ? cmbEmailPrivacy.SelectedIndex : 0;
            bool newAutoLock = cfg.LockEnabled && chkAutoLock != null && chkAutoLock.IsChecked == true;
            if (port == cfg.Port && newToken == cfg.Token && newPcIp == cfg.PcIp
                && mn == cfg.MinLen && mx == cfg.MaxLen && cl == cfg.AutoClearSeconds
                && smsMaxLength == cfg.SmsMaxLength
                && newShowTip == cfg.ShowTip && newStartup == cfg.StartWithWindows
                  && newPrivacy == cfg.PrivacyMode && newEmailPrivacy == cfg.EmailPrivacyMode && newNtfyServer == cfg.NtfyServer
                   && newNtfyTopic == cfg.NtfyTopic && newNtfyToken == cfg.NtfyToken
                  && newFilterKeywords == cfg.FilterKeywords && newFilterRegex == cfg.FilterRegex
                  && newEmailServer == cfg.EmailServer && newEmailAccount == cfg.EmailAccount
                   && newEmailAuth == cfg.EmailAuth && emailPoll == cfg.EmailPollSeconds
                  && newEmailKeywords == cfg.EmailKeywords && newEmailSenders == cfg.EmailSenders
                  && newEmailEnabled == cfg.EmailEnabled && newEmailJunk == cfg.EmailCheckJunk
                   && newEmailNotify == cfg.EmailNotify && newEmailAutoCopy == cfg.EmailAutoCopy
                   && newEmailSaveBody == cfg.EmailSaveBody
                  && newQqPopup == cfg.QqPopupEnabled
                 && newAutoLock == cfg.AutoLockEnabled
                 && !Program.StartupSyncFailed && !settingsRecoveryRequired)
                 {
                 return true;
             }

              lock (Config.SaveGate)
              {
              bool recoveryWasRequired = settingsRecoveryRequired;
              Config previous = new Config();
             previous.CopyFrom(cfg);
              bool startupChanged = previous.StartWithWindows != newStartup;
               bool mailboxChanged = !string.Equals(cfg.EmailServer, newEmailServer, StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(cfg.EmailAccount, newEmailAccount, StringComparison.OrdinalIgnoreCase);
               bool emailRulesChanged = cfg.EmailKeywords != newEmailKeywords || cfg.EmailSenders != newEmailSenders
                   || cfg.MinLen != mn || cfg.MaxLen != mx;
              if (mailboxChanged)
              {
                   cfg.EmailLastUid = cfg.EmailUidValidity = cfg.EmailLastUidJunk = cfg.EmailUidValidityJunk = 0;
                   cfg.EmailMailboxState = "";
                   cfg.EmailAuthEncRaw = "";
                   cfg.EmailAuthUndecryptable = false;
                   cfg.EmailPasswordRequired = false;
              }

             cfg.Port = port;
            cfg.Token = newToken;
            cfg.PcIp = newPcIp;
            cfg.MinLen = mn;
            cfg.MaxLen = mx;
            cfg.SmsMaxLength = smsMaxLength;
            cfg.AutoClearSeconds = cl;
            cfg.ShowTip = newShowTip;
             cfg.StartWithWindows = newStartup;
             cfg.PrivacyMode = newPrivacy;
             cfg.EmailPrivacyMode = newEmailPrivacy;
            cfg.NtfyServer = newNtfyServer;
              cfg.NtfyTopic = newNtfyTopic;
              cfg.NtfyToken = newNtfyToken;
              cfg.FilterKeywords = newFilterKeywords;
              cfg.FilterRegex = newFilterRegex;
              cfg.EmailServer = newEmailServer;
              cfg.EmailAccount = newEmailAccount;
               cfg.EmailAuth = newEmailAuth;
              if (newEmailAuth.Length > 0)
              {
                  // 用户重新填写了授权码：清除“解密失败保留密文”的标记并恢复提示文字。
                   cfg.EmailAuthUndecryptable = false;
                   cfg.EmailAuthEncRaw = "";
                   cfg.EmailPasswordRequired = false;
                  UpdateEmailAuthHint();
              }
              cfg.EmailPollSeconds = emailPoll;
              cfg.EmailKeywords = newEmailKeywords;
              cfg.EmailSenders = newEmailSenders;
              cfg.EmailEnabled = newEmailEnabled;
              cfg.EmailCheckJunk = newEmailJunk;
              cfg.EmailNotify = newEmailNotify;
               cfg.EmailAutoCopy = newEmailAutoCopy;
               cfg.EmailSaveBody = newEmailSaveBody;
              cfg.QqPopupEnabled = newQqPopup;
             cfg.AutoLockEnabled = newAutoLock;

             bool startupAttempted = false;
             bool configSaved = false;
              try
              {
                  cfg.Save(cfgPath);
                  configSaved = true;
                 if (startupChanged || Program.StartupSyncFailed)
                 {
                      startupAttempted = true;
                      Program.SetStartup(cfg.StartWithWindows);
                  }
                  settingsRecoveryRequired = false;
                  // 邮箱 / QQ 弹窗设置即时生效：无需重启程序。
                   Program.RefreshEmailWatchers();
                   if (emailRulesChanged && Program.EmailConfigured()) EmailWatcher.RequestRecentCheck();
                   UpdateEmailAuthHint();
              }
              catch (Exception ex)
              {
                  bool restored = RestoreSettings(previous, startupAttempted, configSaved);
                  settingsRecoveryRequired = !restored || (!configSaved && recoveryWasRequired);
                  Log.Write("apply settings failed: " + ex.GetType().Name);
                  ShowToast(restored
                      ? "保存失败：" + SettingsFailureReason(ex) + "；已还原，可重试或退出"
                      : "恢复未完成：" + SettingsFailureReason(ex) + "；可重试或退出");
                  return false;
             }
              return true;
              }
              }
            finally { applying = false; }
        }

         /// <summary>保存失败时恢复内存、界面、配置文件和已尝试同步的开机启动设置。</summary>
         bool RestoreSettings(Config previous, bool restoreStartup, bool restoreConfig = true)
         {
             bool restored = true;
             lock (Config.SaveGate)
             {
             cfg.CopyFrom(previous);
             try { if (restoreConfig) cfg.Save(cfgPath); }
             catch (Exception ex)
             {
                 restored = false;
                  Log.Write("settings rollback save failed: " + ex.GetType().Name);
             }
             }
             if (restoreStartup)
             {
                 try { Program.SetStartup(previous.StartWithWindows); }
                 catch (Exception ex)
                 {
                     restored = false;
                  Log.Write("settings rollback startup failed: " + ex.GetType().Name);
                 }
             }
             LoadValues();
             return restored;
         }

        static string SettingsFailureReason(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return "配置目录不可写或文件只读";
            if (ex is IOException) return "配置文件被占用或写入失败";
            if (ex is System.Security.Cryptography.CryptographicException) return "Windows 凭据加密失败";
            if (ex is FormatException) return "配置规则无效";
            if (ex is System.Security.SecurityException) return "系统拒绝配置或启动项访问";
            return "设置应用失败（" + ex.GetType().Name + "）";
        }

        // 邮箱服务预设（与下拉顺序一致）；最后一项为自定义。
        static readonly string[] EmailProviderServers = { "imap.qq.com", "imap.163.com", "imap.126.com", "imap.gmail.com", "outlook.office365.com" };

        static int EmailProviderIndexOf(string server)
        {
            for (int i = 0; i < EmailProviderServers.Length; i++)
                if (string.Equals(server, EmailProviderServers[i], StringComparison.OrdinalIgnoreCase)) return i;
            return EmailProviderServers.Length;
        }

        /// <summary>选择服务商预设时自动填服务器地址；“自定义”保持用户输入。</summary>
        void ApplyEmailProviderPreset()
        {
            if (cmbEmailProvider == null || txtEmailServer == null) return;
             int index = cmbEmailProvider.SelectedIndex;
              if (index >= 0 && index < EmailProviderServers.Length)
              {
                  string server = EmailProviderServers[index];
                  if (!string.Equals(server, txtEmailServer.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                  {
                      chkEmail.IsChecked = false;
                      fldEmailAuth.Text = "";
                      txtEmailServer.Text = server;
                  }
              }
             UpdateEmailAuthHint();
         }

         void UpdateEmailAuthHint()
         {
             string server = txtEmailServer == null ? cfg.EmailServer : txtEmailServer.Text.Trim();
             int provider = EmailProviderIndexOf(server);
              bool gmail = provider == 3;
             if (emailAuthHint != null)
                 emailAuthHint.Text = cfg.EmailPasswordRequired ? "请重新填写 IMAP 授权码 / 应用专用密码"
                     : cfg.EmailAuthUndecryptable
                     ? "已失效：换机或换了 Windows 账户，请重新填写"
                     : gmail ? "填写 Google 生成的 16 位应用专用密码，不是账号密码"
                     : "填写 IMAP 授权码 / 应用专用密码，不是邮箱网页登录密码";
             if (emailProviderHint != null)
                 emailProviderHint.Text = gmail
                      ? "Gmail：先开启两步验证，再生成应用专用密码。部分组织账号或高级保护账号无法生成应用专用密码。"
                     : provider >= 0 && provider <= 2
                     ? "QQ / 163 / 126 邮箱：先在网页版邮箱设置中开启 IMAP 服务，再生成授权码。"
                      : "请使用服务商允许的 IMAP 授权码或应用专用密码。";
             if (btnEmailAuthHelp != null)
                 btnEmailAuthHelp.Visibility = gmail ? Visibility.Visible : Visibility.Collapsed;
         }

         static List<string> EmailKeywordList(string text)
         {
             List<string> list = new List<string>();
             HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
             foreach (string line in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
             {
                 string value = line.Trim();
                 if (value.Length > 0 && seen.Add(value)) list.Add(value);
             }
             return list;
         }

         void RenderEmailKeywordTags()
         {
             if (emailKeywordTags == null || txtEmailKeywords == null) return;
             emailKeywordTags.Children.Clear();
             UpdateEmailPreview();
             foreach (string keyword in EmailKeywordList(txtEmailKeywords.Text))
             {
                 string value = keyword;
                 Border chip = new Border { Background = MakeBrush(0xE8, 0xF2, 0xFC), CornerRadius = new CornerRadius(12),
                     Padding = new Thickness(10, 2, 6, 2), Margin = new Thickness(0, 0, 6, 6) };
                 StackPanel content = new StackPanel { Orientation = Orientation.Horizontal };
                 content.Children.Add(new TextBlock { Text = value, Foreground = AccentTx, FontSize = 13,
                     VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 250, ToolTip = value });
                 Button remove = new Button { Content = "×", Width = 24, Height = 24, FontSize = 16,
                     Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = FgFaint,
                     Cursor = Cursors.Hand, ToolTip = "移除关键词", Margin = new Thickness(4, 0, 0, 0) };
                 System.Windows.Automation.AutomationProperties.SetName(remove, "移除关键词 " + value);
                 remove.Click += delegate
                 {
                     List<string> words = EmailKeywordList(txtEmailKeywords.Text);
                     words.Remove(value);
                     txtEmailKeywords.Text = string.Join("\n", words.ToArray());
                     ApplySettings();
                 };
                 content.Children.Add(remove); chip.Child = content; emailKeywordTags.Children.Add(chip);
             }
         }

         void AddEmailKeywords()
         {
             if (txtEmailKeywordAdd == null || txtEmailKeywords == null) return;
             string text = string.Join("\n", EmailKeywordList(txtEmailKeywords.Text + "\n" + txtEmailKeywordAdd.Text).ToArray());
             string normalized, error;
             if (!MailRules.NormalizeLines(text, out normalized, out error)) { ShowToast("主题关键词无效：" + error); return; }
             txtEmailKeywords.Text = normalized;
             txtEmailKeywordAdd.Clear();
             ApplySettings();
         }

         void UpdateEmailPreview()
         {
             if (lblEmailPreviewResult == null || txtEmailSubjectPreview == null) return;
             string normalized, error;
             lblEmailPreviewResult.Text = !MailRules.NormalizeLines(txtEmailKeywords.Text, out normalized, out error)
                 ? "规则无效：" + error : txtEmailSubjectPreview.Text.Length == 0 ? "输入主题查看是否会被接收"
                 : MailRules.SubjectMatches(txtEmailSubjectPreview.Text, normalized) ? "主题命中；仍需满足发件人和时效规则" : "主题未命中，将跳过";
         }

         public void UpdateEmailStatus()
         {
             if (lblEmailStatus == null) return;
             lblEmailStatus.Text = cfg.EmailPasswordRequired ? "请重新填写应用专用密码，邮箱接收已关闭"
                 : !cfg.EmailEnabled ? "邮箱接收已关闭"
                 : !Program.EmailConfigured() ? "邮箱未配置完整" : "收件箱：" + EmailWatcher.Status;
             if (EmailWatcher.LastCheckUtc != DateTime.MinValue)
                 lblEmailStatus.Text += " · 最后检查 " + EmailWatcher.LastCheckUtc.ToLocalTime().ToString("HH:mm:ss");
             if (lblEmailFolders != null) lblEmailFolders.Text = !Program.EmailConfigured() ? "分类目录：待启用"
                 : "分类目录：" + EmailWatcher.FolderStatus + (EmailWatcher.LastFolderCheckUtc == DateTime.MinValue ? ""
                     : " · 最后成功 " + EmailWatcher.LastFolderCheckUtc.ToLocalTime().ToString("HH:mm:ss"));
             if (lblEmailProcessing != null) lblEmailProcessing.Text = cfg.EmailEnabled
                 ? "收件箱处理：" + EmailWatcher.LastMailResult + "\n分类目录处理：" + EmailWatcher.LastFolderResult + "\n" + EmailWatcher.RecentSummary : "";
             Button recheck = Find<Button>((FrameworkElement)Content, "btnEmailRecheck");
             if (recheck != null) { recheck.IsEnabled = Program.EmailConfigured() && !EmailWatcher.RecentCheckPending;
                 recheck.Content = EmailWatcher.RecentCheckPending ? "正在复查近 30 分钟…" : "重新检查近 30 分钟"; }
             string storage = string.Join("\n", new string[] { Config.LastSaveError ?? "", History.LastSaveError ?? "" }).Trim();
             if (lblStorageError != null) lblStorageError.Text = storage;
             Border banner = Find<Border>((FrameworkElement)Content, "storageErrorBanner");
             if (banner != null) banner.Visibility = storage.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
             if (lblQqStatus != null) lblQqStatus.Text = !Program.EmailConfigured() ? "请先配置并启用邮箱；当前不会触发检查"
                 : !cfg.QqPopupEnabled ? "联动已关闭" : "监测中 · 最近提醒 " + QqPopupWatcher.LastSignalText;
             if (chkQqPopup != null) chkQqPopup.IsEnabled = Program.EmailConfigured();
         }

         void TestEmailConnection()
         {
             if (emailTestCancel != null) { emailTestCancel.Cancel(); return; }
             if (!ApplySettings()) return;
             Config identity = new Config(); identity.CopyFrom(cfg);
             if (identity.EmailAccount.Length == 0 || identity.EmailServer.Length == 0
                 || identity.EmailPasswordRequired || identity.EmailAuth.Length == 0)
             { ShowToast("请先填写完整邮箱和 IMAP 授权码 / 应用专用密码"); return; }
             CancellationTokenSource cancel = new CancellationTokenSource(); emailTestCancel = cancel;
             btnEmailTest.Content = "取消测试"; lblEmailTestResult.Text = "正在测试认证与只读收件箱连接…";
             Thread worker = new Thread(delegate()
             {
                 string result;
                 try { result = EmailWatcher.TestConnection(identity, cancel.Token); }
                 catch (OperationCanceledException) { result = "连接测试已取消"; }
                 catch { result = cancel.IsCancellationRequested ? "连接测试已取消" : "连接测试失败，请检查网络、服务设置与授权"; }
                 try { Dispatcher.BeginInvoke(new Action(delegate
                 {
                     emailTestCancel = null; cancel.Dispose(); btnEmailTest.Content = "测试邮箱连接"; lblEmailTestResult.Text = result;
                 })); } catch { cancel.Dispose(); }
             });
             worker.IsBackground = true; worker.Name = "email-test"; worker.Start();
         }

         /// <summary>点"检测本机地址"：只有一个可用地址就直接填入，多个则弹出列表选择。</summary>
         void DetectLocalIp(Button anchor)
         {
             if (txtPcIp == null) return;
              List<string> ips = Program.DetectLocalIpList();
              if (ips.Count == 0)
              {
                  ShowToast("未检测到可用的局域网 IPv4 地址");
                  return;
              }
              if (ips.Count == 1)
              {
                  autoPcIp = "";
                  txtPcIp.Text = ips[0];
                 ApplySettings();
                 return;
             }

             ContextMenu menu = new ContextMenu();
             FrameworkElement host = Content as FrameworkElement;
             if (host != null)
             {
                 menu.Style = host.Resources["FlyoutMenu"] as Style;
                 menu.ItemContainerStyle = host.Resources["FlyoutItem"] as Style;
             }
             foreach (string ip in ips)
             {
                 MenuItem item = new MenuItem();
                 item.Header = ip;
                 string value = ip;
                 if (value == txtPcIp.Text) item.FontWeight = FontWeights.SemiBold;
                   item.Click += delegate
                   {
                       autoPcIp = "";
                       txtPcIp.Text = value;
                      txtPcIp.CaretIndex = value.Length;
                      ApplySettings();
                  };
                 menu.Items.Add(item);
             }

             if (anchor == null)
             {
                 menu.Placement = PlacementMode.MousePoint;
             }
             else
             {
                 menu.PlacementTarget = anchor;
                 menu.Placement = PlacementMode.Bottom;
             }
             menu.IsOpen = true;
         }

         void TestPcConnectivity()
         {
             string ip = txtPcIp == null ? "" : txtPcIp.Text.Trim();
             int port;
              if (!IsUsablePcIp(ip)
                  || !int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
             {
                 MessageBox.Show(this, "请输入有效的 IPv4 地址和端口。", "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }

             SetTestButton(btnTestPc, false, "测试中…");
             string pcToken = fldToken == null ? "" : fldToken.Text.Trim();
             ThreadPool.QueueUserWorkItem(delegate
             {
                 string result;
                 try
                 {
                     HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://" + ip + ":" + port + "/health");
                     request.Method = "GET";
                     request.Timeout = 5000;
                     request.ReadWriteTimeout = 5000;
                     request.UserAgent = "codepass/1.0";
                     request.Proxy = null;
                     if (pcToken.Length > 0)
                         request.Headers["X-Token"] = pcToken;
                     using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                         result = response.StatusCode == HttpStatusCode.OK
                             ? "已连接到 " + ip + ":" + port + "，HTTP 服务和令牌验证正常。"
                             : "服务器已响应，但健康检查结果异常。";
                 }
                  catch (TimeoutException) { result = "连接超时，可能是 EasyTier 路由或 Windows 防火墙未放行。"; }
                  catch (WebException ex)
                  {
                      HttpWebResponse response = ex.Response as HttpWebResponse;
                      result = ex.Status == WebExceptionStatus.Timeout ? "连接超时，可能是 EasyTier 路由或 Windows 防火墙未放行。"
                         : response == null ? "连接失败：" + ex.Message
                         : "服务器返回 HTTP " + (int)response.StatusCode + "，请检查端口或访问令牌。";
                 }
                 catch (Exception ex) { result = "测试失败：" + ex.Message; }
                 Dispatcher.BeginInvoke(new Action(delegate
                 {
                     SetTestButton(btnTestPc, true, "测试连通性");
                     MessageBox.Show(this, result, "电脑地址测试", MessageBoxButton.OK,
                         result.StartsWith("已连接") ? MessageBoxImage.Information : MessageBoxImage.Warning);
                 }));
             });
         }

         void TestNtfyConnectivity()
         {
             string server = txtNtfyServer == null ? "" : txtNtfyServer.Text.Trim().TrimEnd('/');
             string topic = txtNtfyTopic == null ? "" : txtNtfyTopic.Text.Trim();
             Uri uri;
              if (!Uri.TryCreate(server, UriKind.Absolute, out uri)
                   || uri.Scheme != Uri.UriSchemeHttps
                   || uri.Host.Length == 0 || uri.UserInfo.Length > 0)
              {
                  MessageBox.Show(this, "请输入有效的 HTTPS 服务器地址。", "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }
              SetTestButton(btnTestNtfy, false, "测试中…");
             string ntfyToken = fldNtfyToken == null ? "" : fldNtfyToken.Text.Trim();
             ThreadPool.QueueUserWorkItem(delegate
             {
                 string result;
                 try
                 {
                     string target = server + (topic.Length == 0 ? "/" : "/" + topic + "/json");
                      HttpWebRequest request = (HttpWebRequest)WebRequest.Create(target);
                      request.AllowAutoRedirect = false;
                     request.Method = "GET";
                     request.Timeout = 5000;
                     request.ReadWriteTimeout = 5000;
                     request.UserAgent = "codepass/1.0";
                     request.Proxy = null;
                     if (ntfyToken.Length > 0)
                         request.Headers["Authorization"] = "Bearer " + ntfyToken;
                     using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                         result = "服务器可访问，HTTP " + (int)response.StatusCode + " " + response.StatusDescription
                                + (topic.Length == 0 ? "。" : "，主题和鉴权可访问。 ");
                 }
                 catch (WebException ex)
                 {
                     HttpWebResponse response = ex.Response as HttpWebResponse;
                     result = response == null ? "服务器访问失败：" + ex.Message
                         : "服务器返回 HTTP " + (int)response.StatusCode + "，请检查主题或访问令牌。";
                 }
                 catch (Exception ex) { result = "测试失败：" + ex.Message; }
                 Dispatcher.BeginInvoke(new Action(delegate
                 {
                     SetTestButton(btnTestNtfy, true, "测试连通性");
                     MessageBox.Show(this, result, "ntfy 测试", MessageBoxButton.OK,
                         result.StartsWith("服务器可访问") ? MessageBoxImage.Information : MessageBoxImage.Warning);
                 }));
             });
         }

         static void SetTestButton(Button button, bool enabled, string text)
         {
             if (button == null) return;
             button.IsEnabled = enabled;
             button.Content = text;
         }

         void OnExport()
         {
             SaveFileDialog dialog = new SaveFileDialog();
             dialog.Title = "导出 codepass 设置";
             dialog.Filter = "配置文件 (*.ini)|*.ini|所有文件 (*.*)|*.*";
             dialog.FileName = "codepass-config.ini";
             if (dialog.ShowDialog(this) != true) return;

              MessageBoxResult choice = MessageBox.Show(this,
                   "是否包含局域网令牌、ntfy 令牌、邮箱授权码和程序锁校验信息？\r\n\r\n包含敏感信息的文件不要发送给他人。",
                 "导出设置", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
             if (choice == MessageBoxResult.Cancel) return;
             try
             {
                 cfg.Save(dialog.FileName, choice == MessageBoxResult.Yes, true);
                 MessageBox.Show(this, "设置已导出。", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
             }
             catch (Exception ex) { MessageBox.Show(this, "导出失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
         }

         void OnImport()
         {
             OpenFileDialog dialog = new OpenFileDialog();
             dialog.Title = "导入 codepass 设置";
             dialog.Filter = "配置文件 (*.ini)|*.ini|所有文件 (*.*)|*.*";
             if (dialog.ShowDialog(this) != true) return;

              Config imported;
               try { imported = Config.Load(dialog.FileName, true); }
              catch (Exception ex)
              {
                  MessageBox.Show(this, "读取配置失败：" + ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                  return;
              }
                Uri importedNtfyServer;
                bool importedAddressInvalid = imported.PcIp.Length > 0
                    && !IsUsablePcIp(imported.PcIp);
                bool importedNtfyInvalid = imported.NtfyServer.Length == 0 && imported.NtfyTopic.Length > 0;
                if (imported.NtfyServer.Length > 0
                     && (!Uri.TryCreate(imported.NtfyServer, UriKind.Absolute, out importedNtfyServer)
                          || importedNtfyServer.Scheme != Uri.UriSchemeHttps
                          || importedNtfyServer.Host.Length == 0 || importedNtfyServer.UserInfo.Length > 0))
                    importedNtfyInvalid = true;
                string importedFilterError;
                bool importedFilterInvalid = !CodeExtractor.ValidateFilters(
                    imported.FilterKeywords, imported.FilterRegex, out importedFilterError);
                if (imported.Port < 1 || imported.Port > 65535 || imported.MinLen < 1 || imported.MinLen > 32
                     || imported.MaxLen < imported.MinLen || imported.MaxLen > 32
                     || imported.AutoClearSeconds < 0 || imported.PrivacyMode < 0 || imported.PrivacyMode > 2
                     || imported.EmailPrivacyMode < 0 || imported.EmailPrivacyMode > 2
                      || importedAddressInvalid || importedNtfyInvalid || importedFilterInvalid
                    || (imported.SecretsIncluded && imported.LockEnabled && !imported.LockDataValid)
                   || (imported.SecretsIncluded && !imported.LockEnabled
                       && (imported.AutoLockEnabled || imported.LockSalt.Length > 0 || imported.LockHash.Length > 0)))
             {
                    MessageBox.Show(this, importedFilterInvalid
                        ? "配置文件中的信息过滤规则无效：" + importedFilterError
                        : "配置文件中的端口、地址、验证码长度、隐私模式或密码锁数据无效。", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }
              if (MessageBox.Show(this, "导入后需要重启 codepass 才会应用端口和 ntfy 设置，是否继续？", "确认导入",
                      MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
               Config previous = new Config();
               previous.CopyFrom(cfg);
               bool importedSecrets = imported.SecretsIncluded;
              if (!importedSecrets)
              {
                    imported.Token = cfg.Token;
                      imported.NtfyToken = cfg.NtfyToken;
                      imported.NtfyTopic = cfg.NtfyTopic;
                       if (!imported.EmailPasswordRequired && string.Equals(imported.EmailServer, cfg.EmailServer, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(imported.EmailAccount, cfg.EmailAccount, StringComparison.OrdinalIgnoreCase))
                       {
                           imported.EmailAuth = cfg.EmailAuth;
                           imported.EmailAuthEncRaw = cfg.EmailAuthEncRaw;
                           imported.EmailAuthUndecryptable = cfg.EmailAuthUndecryptable;
                       }
                    imported.LockEnabled = cfg.LockEnabled;
                   imported.AutoLockEnabled = cfg.AutoLockEnabled;
                  imported.LockSalt = cfg.LockSalt;
                  imported.LockHash = cfg.LockHash;
               }
               if (!imported.WindowWidthIncluded) imported.WindowWidth = cfg.WindowWidth;
                if (!imported.WindowHeightIncluded) imported.WindowHeight = cfg.WindowHeight;
                lock (Config.SaveGate)
                {
                 imported.EmailLastUid = imported.EmailUidValidity = imported.EmailLastUidJunk = imported.EmailUidValidityJunk = 0;
                 imported.EmailMailboxState = "";
                 cfg.CopyFrom(imported);
                }
              ApplyConfiguredWindowSize();
              LoadValues();
               bool startupChanged = previous.StartWithWindows != cfg.StartWithWindows;
               bool startupAttempted = false;
               try
               {
                   cfg.Save(cfgPath);
                    if (startupChanged || Program.StartupSyncFailed)
                    {
                        startupAttempted = true;
                        Program.SetStartup(cfg.StartWithWindows);
                    }
                    settingsRecoveryRequired = false;
                    Program.RefreshEmailWatchers();
                    MessageBox.Show(this, "设置已导入并保存。" + (importedSecrets ? "" : "\r\n脱敏文件未包含令牌、邮箱授权码和密码锁，当前安全设置已保留。")
                       + "\r\n局域网访问令牌立即生效；端口和 ntfy 设置请重启应用。", "导入成功",
                      MessageBoxButton.OK, MessageBoxImage.Information);
                   if (cfg.ShouldAutoLock) LockWindow();
               }
              catch (Exception ex)
              {
                  bool restored = RestoreSettings(previous, startupAttempted);
                  settingsRecoveryRequired = !restored;
                  MessageBox.Show(this, restored
                      ? "导入保存失败：" + ex.Message + "，已还原。"
                      : "导入保存失败且还原失败，请检查配置文件和开机启动设置。", "错误",
                      MessageBoxButton.OK, MessageBoxImage.Error);
              }
         }

          void ConfigureSecurity()
          {
              if (cfg.LockEnabled)
              {
                  PasswordDialog dialog = new PasswordDialog(this, "验证当前密码", "请输入当前密码",
                      delegate(string password) { return cfg.VerifyPassword(password) ? null : "密码不正确。"; });
                  if (dialog.ShowDialog() != true) { dialog.ClearInput(); return; }
                  dialog.ClearInput();
                  MessageBoxResult action = MessageBox.Show(this, "点击“是”修改密码，点击“否”关闭密码锁。\r\n\r\n密码锁仅保护界面，不加密磁盘文件；自动锁定默认关闭，可在设置页勾选。", "安全性",
                      MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                  if (action == MessageBoxResult.No)
                  {
                      Config previous = new Config();
                      previous.CopyFrom(cfg);
                      cfg.ClearPassword();
                      try { cfg.Save(cfgPath); }
                      catch (Exception ex)
                      {
                          bool restored = RestoreSettings(previous, false);
                          settingsRecoveryRequired = !restored;
                          MessageBox.Show(this, restored
                              ? "保存失败：" + ex.Message + "，已还原。"
                              : "保存失败且还原失败，请检查配置文件。", "安全性", MessageBoxButton.OK, MessageBoxImage.Error);
                          return;
                      }
                      RefreshSecurityControls();
                      settingsRecoveryRequired = false;
                      MessageBox.Show(this, "密码锁已关闭。", "安全性", MessageBoxButton.OK, MessageBoxImage.Information);
                     return;
                 }
                 if (action != MessageBoxResult.Yes) return;
             }

               PasswordDialog first = new PasswordDialog(this, "设置程序密码", "请输入新密码（至少 8 个字符）",
                   delegate(string password) { return password.Length >= 8 ? null : "密码至少需要 8 个字符。"; });
               if (first.ShowDialog() != true) { first.ClearInput(); return; }
               string newPassword = first.Password;
               first.ClearInput();
               PasswordDialog second = new PasswordDialog(this, "确认程序密码", "请再次输入新密码",
                   delegate(string password) { return password == newPassword ? null : "两次输入的密码不一致。"; });
               if (second.ShowDialog() != true) { second.ClearInput(); return; }
               second.ClearInput();
               Config oldSecurity = new Config();
               oldSecurity.CopyFrom(cfg);
              try
              {
                  cfg.SetPassword(newPassword);
                  cfg.Save(cfgPath);
              }
               catch (Exception ex)
               {
                   bool restored = RestoreSettings(oldSecurity, false);
                   settingsRecoveryRequired = !restored;
                   MessageBox.Show(this, restored
                      ? "保存失败：" + ex.Message + "，已还原。"
                      : "保存失败且还原失败，请检查配置文件。", "安全性", MessageBoxButton.OK, MessageBoxImage.Error);
                  return;
               }
              settingsRecoveryRequired = false;
              RefreshSecurityControls();
             MessageBox.Show(this, cfg.AutoLockEnabled
                 ? "密码锁已启用。"
                 : "密码锁已启用；自动锁定保持关闭，可用“立即锁定”手动锁定。", "安全性",
                 MessageBoxButton.OK, MessageBoxImage.Information);
             if (cfg.ShouldAutoLock) LockWindow();
         }

          void LockWindow()
          {
               if (!cfg.LockEnabled || lockOverlay == null) return;
               if (historyRawWindow != null) historyRawWindow.Close();
               if (diagnosticsWindow != null) diagnosticsWindow.Close();
               if (emailTestCancel != null) emailTestCancel.Cancel();
              // 隐藏可能仍在显示的轻提示，避免验证码短暂浮在锁屏上方。
              HideToast();
              lockOverlay.Visibility = Visibility.Visible;
              if (unlockRevealer != null)
                  unlockRevealer.Clear();
              else if (txtUnlockPassword != null)
              {
                  txtUnlockPassword.Password = "";
              }
              if (txtUnlockPassword != null) txtUnlockPassword.Focus();
              if (lblUnlockError != null) lblUnlockError.Text = "";
          }

          void TryUnlock()
          {
              bool verified = txtUnlockPassword != null && cfg.VerifyPassword(txtUnlockPassword.Password);
              if (unlockRevealer != null) unlockRevealer.Clear();
              else if (txtUnlockPassword != null) txtUnlockPassword.Password = "";
              if (!verified)
              {
                  if (lblUnlockError != null) lblUnlockError.Text = "密码不正确，请重试。";
                  return;
              }
              lockOverlay.Visibility = Visibility.Collapsed;
          }

         // ---------- 密钥字段掩码 ----------

         void BuildSecretFields(FrameworkElement root)
         {
             Style passwordStyle = root.Resources["PasswordInput"] as Style;
             Style inputStyle = root.Resources["Input"] as Style;
             Style buttonStyle = root.Resources["SecondaryBtn"] as Style;
             StackPanel tokenHost = Find<StackPanel>(root, "tokenHost");
             StackPanel ntfyHost = Find<StackPanel>(root, "ntfyTokenHost");
             StackPanel emailAuthHost = Find<StackPanel>(root, "emailAuthHost");
             fldToken = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
              fldNtfyToken = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
              fldEmailAuth = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
                 AttachSecret(tokenHost, fldToken, "局域网接收必填");
               AttachSecret(ntfyHost, fldNtfyToken, "私有服务器可选");
               if (emailAuthHost != null) fldEmailAuth.Attach(emailAuthHost);
         }

          static void AttachSecret(StackPanel host, SecretField field, string hint)
          {
              if (host == null) return;
              field.Attach(host);
              host.ToolTip = hint;
         }

         /// <summary>按当前配置刷新密码锁相关控件的可见/可用状态。</summary>
         void RefreshSecurityControls()
         {
             if (chkAutoLock != null)
             {
                 chkAutoLock.IsChecked = cfg.AutoLockEnabled;
                 chkAutoLock.IsEnabled = cfg.LockEnabled;
             }
             if (btnLock != null)
                 btnLock.Visibility = cfg.LockEnabled ? Visibility.Visible : Visibility.Collapsed;
         }

         void OnGeneratePhoneConfig()
         {
              if (txtPcIp == null) return;
             string ip = txtPcIp.Text.Trim();
               if (!ApplySettings()) return;
               if (cfg.Token.Length == 0)
               {
                   ShowToast("请先设置随机访问令牌并重启程序，再复制手机端配置");
                   return;
               }
               if (ip.Length == 0) ip = Program.DetectLocalIp();
              if (!IsUsablePcIp(ip))
             {
                 ShowToast("未检测到可用于手机连接的电脑 IPv4 地址");
                 return;
             }
              string text =
                 "# 复制以下内容填到手机端 forward.sh 顶部\r\n" +
                 "PC_IP=" + ShellLiteral(ip) + "\r\n" +
                 "PC_PORT=" + ShellLiteral(txtPort.Text.Trim()) + "\r\n" +
                 "TOKEN=" + ShellLiteral(fldToken.Text.Trim()) + "\r\n" +
                 "NTFY_SERVER=" + ShellLiteral(txtNtfyServer.Text.Trim()) + "\r\n" +
                 "NTFY_TOPIC=" + ShellLiteral(txtNtfyTopic.Text.Trim()) + "\r\n" +
                 "NTFY_TOKEN=" + ShellLiteral(fldNtfyToken.Text.Trim()) + "\r\n" +
                 "MIN_LEN=" + ShellLiteral(txtMin.Text.Trim()) + "\r\n" +
                 "MAX_LEN=" + ShellLiteral(txtMax.Text.Trim()) + "\r\n";
            bool ok = false;
            try { ok = Program.CopyManually(text, new WindowInteropHelper(this).Handle); } catch { }
             MessageBox.Show(this,
                ok ? text : "复制失败（剪贴板被占用），请重试。",
             ok ? "已复制到剪贴板" : "复制失败", MessageBoxButton.OK,
             ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
         }

          static string ShellLiteral(string value)
         {
             if (value == null) value = "";
             value = value.Replace("\r", " ").Replace("\n", " ");
              return "\"" + value.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("$", "\\$")
                  .Replace("`", "\\`") + "\"";
          }

          static bool IsUsablePcIp(string value)
          {
              IPAddress address;
              return IPAddress.TryParse(value, out address)
                  && address.AddressFamily == AddressFamily.InterNetwork
                  && !IPAddress.IsLoopback(address)
                  && !address.Equals(IPAddress.Any);
          }

        // ---------- 记录 ----------

        void SelectHistoryChannel(string channel)
        {
            historyChannel = channel == "email" ? "email" : channel == "sms" ? "sms" : "all";
            RefreshHistory();
            if (historyScroll != null) historyScroll.ScrollToTop();
        }

        public void RefreshHistory()
        {
            if (listHost == null) return;
            listHost.Children.Clear();
            selectedRow = null;
            int smsCount = 0, emailCount = 0;
            foreach (HistoryItem it in History.Snapshot())
            {
                bool email = it.Channel == "email";
                if (email) emailCount++; else smsCount++;
                if (HistorySearch.Matches(it, historyChannel, txtHistorySearch == null ? "" : txtHistorySearch.Text))
                    listHost.Children.Add(BuildRow(it));
            }
            SetNavState(tabSmsHistory, historyChannel == "sms");
            SetNavState(tabEmailHistory, historyChannel == "email");
            SetNavState(tabAllHistory, historyChannel == "all");
            if (tabAllHistory != null) tabAllHistory.Content = "全部 (" + (smsCount + emailCount) + ")";
            if (tabSmsHistory != null) tabSmsHistory.Content = "短信 (" + smsCount + ")";
            if (tabEmailHistory != null) tabEmailHistory.Content = "邮件 (" + emailCount + ")";
            if (lblCount != null)
                lblCount.Text = "显示 " + listHost.Children.Count + " / " + (smsCount + emailCount) + " 条 · 最多 " + History.Max + " 条";
            if (lblHistoryEmpty != null)
            {
                lblHistoryEmpty.Text = txtHistorySearch != null && txtHistorySearch.Text.Trim().Length > 0
                    ? "没有匹配的记录，请调整搜索词或重置筛选"
                    : historyChannel == "email" ? "暂无邮箱验证码记录" : historyChannel == "sms" ? "暂无短信验证码记录" : "暂无验证码记录";
                lblHistoryEmpty.Visibility = listHost.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        Border BuildRow(HistoryItem it)
        {
            Grid g = new Grid();
            AddCol(g, 128, false);
            AddCol(g, 1, true);
            AddCol(g, 100, false);
            AddCol(g, 2, true);

            g.Children.Add(Cell(it.Time, 0, FgBody, false));
            TextBlock senderCell = Cell(it.Source, 1, FgBody, false);
            senderCell.ToolTip = String.IsNullOrEmpty(it.Source) ? null : it.Source;
            g.Children.Add(senderCell);
            TextBlock codeCell = Cell(it.Code, 2, AccentTx, true);
            g.Children.Add(codeCell);
              TextBlock rawCell = Cell(string.IsNullOrEmpty(it.Raw) && it.Channel == "email" ? "未保存正文" : it.Raw, 3, FgFaint, false);
             rawCell.ToolTip = new ToolTip
             {
                 MaxWidth = 560,
                 Content = new ScrollViewer { MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                     Content = new TextBlock { Text = HistoryRawText(it), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 } }
             };
             ToolTipService.SetInitialShowDelay(rawCell, 300);
             ToolTipService.SetShowDuration(rawCell, 30000);
             rawCell.Background = Brushes.Transparent;
             rawCell.Cursor = Cursors.Hand;
             rawCell.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
             {
                 if (args.ClickCount != 2) return;
                 ShowHistoryRaw(it);
                 args.Handled = true;
             };
             g.Children.Add(rawCell);

            Border row = new Border();
            row.Height = 40;
            row.Margin = new Thickness(0, 2, 0, 2);
            row.CornerRadius = new CornerRadius(4);
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;
            row.Child = g;
            row.ToolTip = "单击验证码可复制；双击原文查看全部内容";

            string code = it.Code;
            codeCell.Cursor = Cursors.Hand;
            codeCell.ToolTip = it.CanBeLatest ? "单击复制验证码" : "仅历史：复查/首次补查不更新最新验证码；仍可单击手动复制";
            codeCell.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (string.IsNullOrEmpty(code)) return;
                // 只复制验证码；点击“原文”等区域不再占用剪贴板（避免点击卡顿）。
                try
                {
                    if (Program.CopyManually(code, new WindowInteropHelper(this).Handle)) ShowToast("已复制验证码 " + code);
                    else ShowToast("复制失败，剪贴板被占用，请重试");
                }
                catch { ShowToast("复制失败，剪贴板被占用，请重试"); }
            };
            row.MouseEnter += delegate { if (row != selectedRow) row.Background = RowHover; };
            row.MouseLeave += delegate { if (row != selectedRow) row.Background = Brushes.Transparent; };
            row.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (selectedRow != null) selectedRow.Background = Brushes.Transparent;
                selectedRow = row;
                row.Background = RowSel;
            };
            return row;
        }

        static void AddCol(Grid g, double width, bool star)
        {
            ColumnDefinition c = new ColumnDefinition();
            if (star) c.Width = new GridLength(width > 0 ? width : 1, GridUnitType.Star);
            else c.Width = new GridLength(width);
            g.ColumnDefinitions.Add(c);
        }

        static TextBlock Cell(string text, int col, Brush fg, bool bold)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = 13;
            t.Foreground = fg;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            if (col == 1 || col == 3)
            {
                t.HorizontalAlignment = HorizontalAlignment.Stretch;
                t.TextAlignment = col == 1 ? TextAlignment.Center : TextAlignment.Left;
                t.Margin = new Thickness(8, 0, 8, 0);
            }
            if (bold) t.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(t, col);
            return t;
        }

        static string HistoryRawText(HistoryItem item)
        {
            if (!string.IsNullOrEmpty(item.Raw)) return item.Raw;
            return item.Channel == "email" ? "这条邮件未保存正文。可在邮箱设置中开启“在记录中保存邮件内容”，仅对之后收到的邮件生效。"
                : "这条记录没有原文内容。";
        }

        Window CreateHistoryRawWindow(HistoryItem item)
        {
            Window dialog = new Window
            {
                Title = "记录原文", Width = 760, Height = 520, MinWidth = 480, MinHeight = 300,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false, ResizeMode = ResizeMode.CanResize,
                Background = MakeBrush(0xF3, 0xF3, 0xF3),
                FontFamily = FontFamily, FontSize = 14, Icon = Icon
            };
            Grid layout = new Grid { Margin = new Thickness(20) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.Children.Add(new TextBlock { Text = item.Time + "  ·  " + item.Source,
                TextWrapping = TextWrapping.Wrap, Foreground = FgBody, Margin = new Thickness(0, 0, 0, 12) });
            TextBox original = new TextBox
            {
                Name = "txtHistoryRawFull", Text = HistoryRawText(item), IsReadOnly = true,
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(12), Background = Brushes.White, Foreground = FgBody,
                BorderBrush = MakeBrush(0xDD, 0xDD, 0xDD), BorderThickness = new Thickness(1)
            };
            Grid.SetRow(original, 1); layout.Children.Add(original);
            Button close = new Button { Content = "关闭", MinWidth = 84, Height = 34,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            FrameworkElement root = Content as FrameworkElement;
            if (root != null) close.Style = root.Resources["SecondaryBtn"] as Style;
            close.Click += delegate { dialog.Close(); };
            Grid.SetRow(close, 2); layout.Children.Add(close);
            dialog.Content = layout;
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key == Key.Escape) { dialog.Close(); args.Handled = true; }
            };
            historyRawWindow = dialog;
            dialog.Closed += delegate { if (historyRawWindow == dialog) historyRawWindow = null; };
            dialog.StateChanged += delegate
            {
                if (dialog.WindowState == WindowState.Minimized && cfg.ShouldAutoLock) LockWindow();
            };
            return dialog;
        }

        void ShowHistoryRaw(HistoryItem item)
        {
            if (lockOverlay != null && lockOverlay.Visibility == Visibility.Visible) return;
            Window dialog = CreateHistoryRawWindow(item);
            dialog.ShowDialog();
        }

        public void OnNewRecord(HistoryItem it)
        {
            // 另一分类新增记录也可能淘汰当前分类的最旧记录，始终以历史快照刷新。
            RefreshHistory();
        }

        // 页面内轻提示：显示约 1.4 秒后淡出, 用于复制成功/失败反馈。
        void ShowToast(string message)
        {
            if (toastBox == null) return;
            if (toastText != null) toastText.Text = message;
            toastBox.BeginAnimation(UIElement.OpacityProperty, null);
            toastBox.Opacity = 1;
            toastBox.Visibility = Visibility.Visible;
            if (toastTimer == null)
            {
                toastTimer = new DispatcherTimer();
                toastTimer.Interval = TimeSpan.FromMilliseconds(1400);
                toastTimer.Tick += delegate
                {
                    toastTimer.Stop();
                    if (toastBox == null) return;
                    DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                    fade.Completed += delegate
                    {
                        if (toastBox != null) toastBox.Visibility = Visibility.Collapsed;
                    };
                    toastBox.BeginAnimation(UIElement.OpacityProperty, fade);
                };
            }
            toastTimer.Stop();
            toastTimer.Start();
        }

        // 立即隐藏轻提示（例如锁定窗口前）。
        void HideToast()
        {
            if (toastTimer != null) toastTimer.Stop();
            if (toastBox == null) return;
            toastBox.BeginAnimation(UIElement.OpacityProperty, null);
            toastBox.Opacity = 1;
            toastBox.Visibility = Visibility.Collapsed;
        }

        // ---------- 关于 / 检查更新 ----------

        void OpenRepoPage()
        {
            if (Updater.IsPlaceholderRepo)
            {
                MessageBox.Show(this, "尚未配置 GitHub 仓库地址，发布前请在源码中替换占位地址。",
                    "codepass", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(Updater.RepoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开浏览器：" + ex.Message, "codepass",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

         void CheckUpdate()
         {
             if (updateBusy) return;
             updateBusy = true;
              if (btnUpdate != null) btnUpdate.IsEnabled = false;
             SetUpdateStatus("正在检查更新…");
             Updater.CheckForUpdate(
                 delegate(string s) { SetUpdateStatus(s); },
                 delegate
                {
                    // 未更新或更新失败：恢复按钮
                     updateBusy = false;
                     Dispatcher.BeginInvoke(new Action(delegate
                     {
                          if (btnUpdate != null) btnUpdate.IsEnabled = true;
                     }));
                 });
         }

         void SetUpdateStatus(string text)
         {
             if (!Dispatcher.CheckAccess())
             {
                 Dispatcher.BeginInvoke(new Action(delegate { SetUpdateStatus(text); }));
                 return;
             }
             if (lblUpdateStatus != null) lblUpdateStatus.Text = text;
         }

        // ---------- 托盘窗口行为 ----------

        public void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
        }

         public bool ExitApp()
         {
             allowClose = true;
             closeCompleted = false;
             Close();
             return closeCompleted;
         }

          protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
          {
               if (emailTestCancel != null) emailTestCancel.Cancel();
               // 保存失败会还原输入并提示；不能因此阻止关闭/托盘退出或系统关机。
               if (!SaveHistoryRetention()) Log.Write("closing with history retention setting not saved");
               if (!ApplySettings()) Log.Write("closing with settings not saved; previous configuration retained");
              SaveWindowSize();
              if (!allowClose)
             {
                 e.Cancel = true;
                 if (cfg.ShouldAutoLock) LockWindow();
                 ShowInTaskbar = false;
                 Hide();
                 return;
              }
              base.OnClosing(e);
          }

          void ScheduleWindowSizeSave()
          {
              if (!IsLoaded || WindowState != WindowState.Normal) return;
              if (windowSizeTimer == null)
              {
                  windowSizeTimer = new DispatcherTimer();
                  windowSizeTimer.Interval = TimeSpan.FromMilliseconds(450);
                  windowSizeTimer.Tick += delegate
                  {
                      windowSizeTimer.Stop();
                      SaveWindowSize();
                  };
              }
              windowSizeTimer.Stop();
              windowSizeTimer.Start();
          }

          void ApplyConfiguredWindowSize()
          {
              int workWidth = (int)SystemParameters.WorkArea.Width;
              int workHeight = (int)SystemParameters.WorkArea.Height;
              int savedWidth = cfg.WindowWidth < MinWidth ? 1100 : cfg.WindowWidth;
              int savedHeight = cfg.WindowHeight < MinHeight ? 760 : cfg.WindowHeight;
              Width = Math.Min(Math.Max(savedWidth, (int)MinWidth), Math.Max((int)MinWidth, workWidth - 24));
              Height = Math.Min(Math.Max(savedHeight, (int)MinHeight), Math.Max((int)MinHeight, workHeight - 24));
          }

          void SaveWindowSize()
          {
              if (WindowState != WindowState.Normal || Width < MinWidth || Height < MinHeight) return;
              int width = (int)Math.Round(Width);
              int height = (int)Math.Round(Height);
              if (width == cfg.WindowWidth && height == cfg.WindowHeight) return;
              cfg.WindowWidth = width;
              cfg.WindowHeight = height;
              try { cfg.Save(cfgPath); }
              catch (Exception ex) { Log.Write("window size save failed: " + ex.GetType().Name); }
          }

          protected override void OnClosed(EventArgs e)
          {
              closeCompleted = true;
              base.OnClosed(e);
          }
      }

    /// <summary>默认掩码、可切换显示的密钥输入框（PasswordBox 与 TextBox 叠放切换）。</summary>
    sealed class SecretField
    {
        readonly PasswordBox masked = new PasswordBox();
        readonly TextBox plain = new TextBox();
        readonly Button toggle = new Button();
        bool revealed;

        public event Action Committed;
        public SecretField(Style passwordStyle, Style textStyle, Style buttonStyle, double width)
        {
            masked.Style = passwordStyle;
            masked.Width = width;
            masked.VerticalAlignment = VerticalAlignment.Center;
            plain.Style = textStyle;
            plain.Width = width;
            plain.VerticalAlignment = VerticalAlignment.Center;
            plain.Visibility = Visibility.Collapsed;
            toggle.Style = buttonStyle;
            toggle.Content = "显示";
            toggle.Width = 68;
            toggle.Height = 32;
            toggle.Margin = new Thickness(8, 0, 0, 0);
            toggle.VerticalAlignment = VerticalAlignment.Center;

            toggle.Click += delegate { revealed = !revealed; Apply(); };
            masked.PasswordChanged += delegate { if (!revealed) plain.Text = masked.Password; };
            plain.TextChanged += delegate { if (revealed) masked.Password = plain.Text; };
             masked.LostFocus += delegate { if (Committed != null) Committed(); };
             plain.LostFocus += delegate { if (Committed != null) Committed(); };
             KeyEventHandler onEnter = delegate(object s, KeyEventArgs e) { if (e.Key == Key.Return && Committed != null) Committed(); };
             masked.KeyDown += onEnter;
             plain.KeyDown += onEnter;
        }

        public string Text
        {
            get { return revealed ? plain.Text : masked.Password; }
            set
            {
                string v = value ?? "";
                masked.Password = v;
                plain.Text = v;
            }
        }

        public void Attach(Panel host)
        {
            host.Children.Add(masked);
            host.Children.Add(plain);
            host.Children.Add(toggle);
        }

        void Apply()
        {
            if (revealed)
            {
                plain.Text = masked.Password;
                plain.Visibility = Visibility.Visible;
                masked.Visibility = Visibility.Collapsed;
                toggle.Content = "隐藏";
                plain.Focus();
                plain.CaretIndex = plain.Text.Length;
            }
            else
            {
                masked.Password = plain.Text;
                masked.Visibility = Visibility.Visible;
                plain.Visibility = Visibility.Collapsed;
                toggle.Content = "显示";
                masked.Focus();
            }
        }
    }

      /// <summary>密码框的显示/隐藏按钮：同位置切换明文框和掩码框，两边内容始终同步。</summary>
      sealed class PasswordRevealer
      {
          readonly PasswordBox masked;
          readonly TextBox plain;
          readonly Button toggle;
          bool revealed;
          bool syncing;

            public PasswordRevealer(PasswordBox masked, TextBox plain, Button toggle)
          {
              this.masked = masked;
              this.plain = plain;
              this.toggle = toggle;
               plain.Visibility = Visibility.Collapsed;
               plain.Text = masked.Password;
               UpdateToggleVisual();
              masked.PasswordChanged += delegate
              {
                  if (syncing || revealed) return;
                  syncing = true;
                  plain.Text = masked.Password;
                  syncing = false;
              };
              plain.TextChanged += delegate
              {
                  if (syncing || !revealed) return;
                  syncing = true;
                  masked.Password = plain.Text;
                  syncing = false;
              };
                toggle.Click += delegate { revealed = !revealed; Apply(); };
           }

           public void Clear()
           {
               syncing = true;
               masked.Password = "";
               plain.Text = "";
               syncing = false;
               revealed = false;
               plain.Visibility = Visibility.Collapsed;
               masked.Visibility = Visibility.Visible;
                UpdateToggleVisual();
           }

          void Apply()
          {
              if (revealed)
              {
                  plain.Text = masked.Password;
                  masked.Visibility = Visibility.Collapsed;
                  plain.Visibility = Visibility.Visible;
                  plain.Focus();
                  plain.CaretIndex = plain.Text.Length;
              }
              else
              {
                  masked.Password = plain.Text;
                  plain.Visibility = Visibility.Collapsed;
                  masked.Visibility = Visibility.Visible;
                  masked.Focus();
              }
                UpdateToggleVisual();
            }

            void UpdateToggleVisual()
            {
                toggle.Content = revealed ? "隐藏" : "显示";
                toggle.ToolTip = revealed ? "隐藏密码" : "显示密码";
            }
      }

      sealed class CodepassDialog : Window
      {
          readonly MessageBoxButton buttonSet;
          MessageBoxResult result = MessageBoxResult.Cancel;

          public static MessageBoxResult Show(Window owner, string message, string title,
              MessageBoxButton buttons, MessageBoxImage image)
          {
              CodepassDialog dialog = new CodepassDialog(owner, message, title, buttons, image);
              dialog.ShowDialog();
              return dialog.result;
          }

          CodepassDialog(Window owner, string message, string title,
              MessageBoxButton buttons, MessageBoxImage image)
          {
              buttonSet = buttons;
              Owner = owner;
              Title = title;
              Width = 470;
              SizeToContent = SizeToContent.Height;
               WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
               ResizeMode = ResizeMode.NoResize;
              WindowStyle = WindowStyle.None;
              AllowsTransparency = true;
              Background = Brushes.Transparent;
              ShowInTaskbar = false;
              FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
              FontSize = 13.5;
              if (owner != null) Icon = owner.Icon;

              // 阴影单独一层, 内容层不套 Effect(否则整窗文字被栅格化变糊)
              Border shellShadow = new Border();
              shellShadow.Margin = new Thickness(12);
              shellShadow.Background = MakeBrush(0xF3, 0xF3, 0xF3);
              shellShadow.CornerRadius = new CornerRadius(8);
              shellShadow.IsHitTestVisible = false;
              shellShadow.Effect = new System.Windows.Media.Effects.DropShadowEffect
              {
                  BlurRadius = 18,
                  ShadowDepth = 0,
                  Opacity = 0.12,
                  Color = Color.FromRgb(0x1A, 0x1A, 0x1A)
              };

              Border shell = new Border();
              shell.Margin = new Thickness(12);
              shell.Background = MakeBrush(0xF3, 0xF3, 0xF3);
              shell.BorderBrush = MakeBrush(0xEB, 0xEB, 0xEB);
              shell.BorderThickness = new Thickness(1);
              shell.CornerRadius = new CornerRadius(8);

              Grid layout = new Grid();
              layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
              layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
              layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

              Grid titleBar = new Grid();
              titleBar.Margin = new Thickness(18, 0, 8, 0);
              titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
              {
                  if (e.ButtonState == MouseButtonState.Pressed)
                  {
                      try { DragMove(); } catch { }
                  }
              };
              TextBlock titleText = new TextBlock();
              titleText.Text = title;
              titleText.FontSize = 14.5;
              titleText.FontWeight = FontWeights.SemiBold;
              titleText.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              titleText.VerticalAlignment = VerticalAlignment.Center;
              titleBar.Children.Add(titleText);
              Button close = MakeCloseButton();
              close.HorizontalAlignment = HorizontalAlignment.Right;
              close.VerticalAlignment = VerticalAlignment.Center;
              close.Click += delegate { Finish(MessageBoxResult.Cancel); };
              titleBar.Children.Add(close);
              Grid.SetRow(titleBar, 0);
              layout.Children.Add(titleBar);

              StackPanel body = new StackPanel();
              body.Orientation = Orientation.Horizontal;
              body.Margin = new Thickness(24, 8, 24, 20);
              Border badge = MakeBadge(image);
              body.Children.Add(badge);
              TextBlock messageText = new TextBlock();
              messageText.Text = message ?? "";
              messageText.TextWrapping = TextWrapping.Wrap;
              messageText.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              messageText.VerticalAlignment = VerticalAlignment.Center;
              messageText.Margin = new Thickness(14, 0, 0, 0);
              messageText.MaxWidth = 352;
              body.Children.Add(messageText);
              Grid.SetRow(body, 1);
              layout.Children.Add(body);

              StackPanel buttonsPanel = new StackPanel();
              buttonsPanel.Orientation = Orientation.Horizontal;
              buttonsPanel.HorizontalAlignment = HorizontalAlignment.Right;
              buttonsPanel.Margin = new Thickness(24, 0, 24, 22);
              AddButtons(buttonsPanel, buttons);
              Grid.SetRow(buttonsPanel, 2);
              layout.Children.Add(buttonsPanel);

              shell.Child = layout;
              Grid host = new Grid();
              host.Children.Add(shellShadow);
              host.Children.Add(shell);
              Content = host;
              Loaded += delegate { DisplayMotion.Reveal(this, layout); };
              PreviewKeyDown += OnPreviewKeyDown;
          }

          static SolidColorBrush MakeBrush(byte r, byte g, byte b)
          {
              SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
              brush.Freeze();
              return brush;
          }

          Border MakeBadge(MessageBoxImage image)
          {
              string glyph = "i";
              Brush background = MakeBrush(0x00, 0x78, 0xD4);
              if (image == MessageBoxImage.Warning)
              {
                  glyph = "!";
                  background = MakeBrush(0xC8, 0x6A, 0x00);
              }
              else if (image == MessageBoxImage.Error)
              {
                  glyph = "×";
                  background = MakeBrush(0xC4, 0x2B, 0x1C);
              }
              else if (image == MessageBoxImage.Question)
              {
                  glyph = "?";
              }

              Border badge = new Border();
              badge.Width = 30;
              badge.Height = 30;
              badge.CornerRadius = new CornerRadius(15);
              badge.Background = background;
              badge.VerticalAlignment = VerticalAlignment.Center;
              TextBlock text = new TextBlock();
              text.Text = glyph;
              text.Foreground = Brushes.White;
              text.FontSize = 18;
              text.FontWeight = FontWeights.Bold;
              text.HorizontalAlignment = HorizontalAlignment.Center;
              text.VerticalAlignment = VerticalAlignment.Center;
              text.TextAlignment = TextAlignment.Center;
              badge.Child = text;
              return badge;
          }

          void AddButtons(StackPanel panel, MessageBoxButton buttons)
          {
              if (buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel)
              {
                  AddButton(panel, "确定", MessageBoxResult.OK, true);
              }
              else
              {
                  AddButton(panel, "是", MessageBoxResult.Yes, true);
                  AddButton(panel, "否", MessageBoxResult.No, false);
              }
              if (buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel)
                  AddButton(panel, "取消", MessageBoxResult.Cancel, false);
          }

          void AddButton(StackPanel panel, string text, MessageBoxResult value, bool primary)
          {
              Button button = MakeRoundedButton(text, primary);
              button.IsDefault = primary;
              button.IsCancel = value == MessageBoxResult.Cancel;
              button.Margin = new Thickness(8, 0, 0, 0);
              button.Click += delegate { Finish(value); };
              panel.Children.Add(button);
          }

          void Finish(MessageBoxResult value)
          {
              result = value;
              DialogResult = true;
          }

          void OnPreviewKeyDown(object sender, KeyEventArgs e)
          {
              if (e.Key == Key.Escape)
              {
                  Finish(MessageBoxResult.Cancel);
                  e.Handled = true;
              }
              else if (e.Key == Key.Enter)
              {
                  Button focused = Keyboard.FocusedElement as Button;
                  if (focused != null)
                  {
                      focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, focused));
                      e.Handled = true;
                      return;
                  }
                  MessageBoxResult value = buttonSet == MessageBoxButton.OK || buttonSet == MessageBoxButton.OKCancel
                      ? MessageBoxResult.OK : MessageBoxResult.Yes;
                  Finish(value);
                  e.Handled = true;
              }
          }

          internal static Button MakeRoundedButton(string text, bool primary)
          {
              Button button = new Button();
              button.Width = primary ? 88 : 78;
              button.Height = 32;
              button.Padding = new Thickness(0);
              button.Background = Brushes.Transparent;
              button.BorderThickness = new Thickness(0);
              button.FocusVisualStyle = null;
              button.Cursor = Cursors.Hand;
              button.Template = CreateBareButtonTemplate();
              Border border = new Border();
              border.CornerRadius = new CornerRadius(4);
              border.Padding = new Thickness(18, 0, 18, 0);
              Brush normalBackground = primary ? MakeBrush(0x00, 0x78, 0xD4) : Brushes.White;
              Brush hoverBackground = primary ? MakeBrush(0x1A, 0x84, 0xD5) : MakeBrush(0xF6, 0xF6, 0xF6);
              Brush pressedBackground = primary ? MakeBrush(0x33, 0x93, 0xDA) : MakeBrush(0xED, 0xED, 0xED);
              Brush normalBorder = primary ? Brushes.Transparent : MakeBrush(0xEB, 0xEB, 0xEB);
              Brush hoverBorder = primary ? Brushes.Transparent : MakeBrush(0xEB, 0xEB, 0xEB);
              Brush pressedBorder = primary ? Brushes.Transparent : MakeBrush(0xEB, 0xEB, 0xEB);
              border.Background = normalBackground;
              border.BorderBrush = normalBorder;
              border.BorderThickness = primary ? new Thickness(0) : new Thickness(1);
              TextBlock label = new TextBlock();
              label.Text = text;
              label.FontSize = 13.5;
              label.Foreground = primary ? Brushes.White : MakeBrush(0x1C, 0x1C, 0x1C);
              label.HorizontalAlignment = HorizontalAlignment.Center;
              label.VerticalAlignment = VerticalAlignment.Center;
              border.Child = label;
              button.Content = border;
              button.MouseEnter += delegate
              {
                  border.Background = hoverBackground;
                  border.BorderBrush = hoverBorder;
              };
              button.MouseLeave += delegate
              {
                  border.Background = normalBackground;
                  border.BorderBrush = normalBorder;
              };
              button.PreviewMouseLeftButtonDown += delegate
              {
                  border.Background = pressedBackground;
                  border.BorderBrush = pressedBorder;
              };
              button.PreviewMouseLeftButtonUp += delegate
              {
                  border.Background = button.IsMouseOver ? hoverBackground : normalBackground;
                  border.BorderBrush = button.IsMouseOver ? hoverBorder : normalBorder;
              };
              return button;
          }

          static Button MakeCloseButton()
          {
              Button button = new Button();
              button.Width = 34;
              button.Height = 30;
              button.Padding = new Thickness(0);
              button.Background = Brushes.Transparent;
              button.BorderThickness = new Thickness(0);
              button.FocusVisualStyle = null;
              button.Cursor = Cursors.Hand;
              button.Template = CreateBareButtonTemplate();
              Border border = new Border();
              border.CornerRadius = new CornerRadius(4);
              TextBlock text = new TextBlock();
              text.Text = "×";
              text.FontSize = 18;
              text.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              text.HorizontalAlignment = HorizontalAlignment.Center;
              text.VerticalAlignment = VerticalAlignment.Center;
              border.Child = text;
              button.Content = border;
              return button;
          }

           internal static ControlTemplate CreateBareButtonTemplate()
          {
              ControlTemplate template = new ControlTemplate(typeof(Button));
              FrameworkElementFactory root = new FrameworkElementFactory(typeof(Border));
              root.SetValue(Border.BackgroundProperty, Brushes.Transparent);
              root.SetValue(Border.BorderThicknessProperty, new Thickness(0));
              FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
              content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
              content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
              content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
              root.AppendChild(content);
              template.VisualTree = root;
              return template;
          }

          internal static ControlTemplate CreatePasswordTemplate()
           {
               return CreateFieldTemplate(typeof(PasswordBox));
           }

            // 密码输入框的基础模板
            internal static ControlTemplate CreateFieldTemplate(Type targetType)
           {
              ControlTemplate template = new ControlTemplate(targetType);
              FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
              border.SetValue(Border.BackgroundProperty, MakeBrush(0xFB, 0xFB, 0xFB));
              border.SetValue(Border.BorderBrushProperty, MakeBrush(0xEB, 0xEB, 0xEB));
              border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
              border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
              FrameworkElementFactory host = new FrameworkElementFactory(typeof(ScrollViewer));
              host.Name = "PART_ContentHost";
              host.SetValue(ScrollViewer.FocusableProperty, false);
                host.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 0, 7, 0));
              host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
              border.AppendChild(host);
              template.VisualTree = border;
              return template;
          }
      }

      sealed class PasswordDialog : Window
      {
          readonly PasswordBox box;
          readonly PasswordRevealer revealer;
          readonly Func<string, string> validator;

          public string Password { get { return box == null ? "" : box.Password; } }

          public PasswordDialog(Window owner, string title, string hint, Func<string, string> validator)
          {
              this.validator = validator;
              Owner = owner;
              Title = title;
              Width = 390;
              SizeToContent = SizeToContent.Height;
              WindowStartupLocation = WindowStartupLocation.CenterOwner;
              ResizeMode = ResizeMode.NoResize;
              WindowStyle = WindowStyle.None;
              AllowsTransparency = true;
              Background = Brushes.Transparent;
              ShowInTaskbar = false;
              FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI, Segoe UI");
              FontSize = 13.5;
              if (owner != null) Icon = owner.Icon;

              // 阴影单独一层, 内容层不套 Effect(否则整窗文字被栅格化变糊)
              Border shellShadow = new Border();
              shellShadow.Margin = new Thickness(12);
              shellShadow.Background = MakeBrush(0xF3, 0xF3, 0xF3);
              shellShadow.CornerRadius = new CornerRadius(8);
              shellShadow.IsHitTestVisible = false;
              shellShadow.Effect = new System.Windows.Media.Effects.DropShadowEffect
              {
                  BlurRadius = 18,
                  ShadowDepth = 0,
                  Opacity = 0.12,
                  Color = Color.FromRgb(0x1A, 0x1A, 0x1A)
              };

              Border shell = new Border();
              shell.Margin = new Thickness(12);
              shell.Background = MakeBrush(0xF3, 0xF3, 0xF3);
              shell.BorderBrush = MakeBrush(0xEB, 0xEB, 0xEB);
              shell.BorderThickness = new Thickness(1);
              shell.CornerRadius = new CornerRadius(8);

              Grid layout = new Grid();
              layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
              layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
              layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

              Grid titleBar = new Grid();
              titleBar.Margin = new Thickness(18, 0, 8, 0);
              titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
              {
                  if (e.ButtonState == MouseButtonState.Pressed)
                  {
                      try { DragMove(); } catch { }
                  }
              };
              TextBlock titleText = new TextBlock();
              titleText.Text = title;
              titleText.FontSize = 14.5;
              titleText.FontWeight = FontWeights.SemiBold;
              titleText.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              titleText.VerticalAlignment = VerticalAlignment.Center;
              titleBar.Children.Add(titleText);
              Button close = MakeCloseButton();
              close.HorizontalAlignment = HorizontalAlignment.Right;
              close.VerticalAlignment = VerticalAlignment.Center;
              close.Click += delegate { DialogResult = false; };
              titleBar.Children.Add(close);
              Grid.SetRow(titleBar, 0);
              layout.Children.Add(titleBar);

              StackPanel panel = new StackPanel();
              panel.Margin = new Thickness(24, 8, 24, 0);

              TextBlock label = new TextBlock();
              label.Text = hint;
              label.Foreground = MakeBrush(0x61, 0x61, 0x61);
              panel.Children.Add(label);

               StackPanel field = new StackPanel();
               field.Orientation = Orientation.Horizontal;
               field.Margin = new Thickness(0, 14, 0, 0);

               box = new PasswordBox();
               box.Template = CodepassDialog.CreatePasswordTemplate();
               box.Height = 32;
               box.Width = 240;
               box.VerticalContentAlignment = VerticalAlignment.Center;
               field.Children.Add(box);

               TextBox plain = new TextBox();
               plain.Template = CodepassDialog.CreateFieldTemplate(typeof(TextBox));
               plain.Height = 32;
               plain.Width = 240;
               plain.VerticalContentAlignment = VerticalAlignment.Center;
               plain.Visibility = Visibility.Collapsed;
               field.Children.Add(plain);

               Button toggle = new Button();
               FrameworkElement ownerRoot = owner == null ? null : owner.Content as FrameworkElement;
               toggle.Style = ownerRoot == null ? null : ownerRoot.TryFindResource("SecondaryBtn") as Style;
               toggle.Content = "显示";
               toggle.Width = 68;
               toggle.Margin = new Thickness(8, 0, 0, 0);
               toggle.VerticalAlignment = VerticalAlignment.Center;
               field.Children.Add(toggle);

                revealer = new PasswordRevealer(box, plain, toggle);

              panel.Children.Add(field);
              Grid.SetRow(panel, 1);
              layout.Children.Add(panel);

              StackPanel buttons = new StackPanel();
              buttons.Orientation = Orientation.Horizontal;
              buttons.HorizontalAlignment = HorizontalAlignment.Right;
              buttons.Margin = new Thickness(24, 14, 24, 22);
              Button cancel = CodepassDialog.MakeRoundedButton("取消", false);
              cancel.Margin = new Thickness(8, 0, 0, 0);
              cancel.Click += delegate { DialogResult = false; };
              buttons.Children.Add(cancel);
              Button ok = CodepassDialog.MakeRoundedButton("确定", true);
              ok.Margin = new Thickness(8, 0, 0, 0);
              ok.Click += delegate { TryConfirm(); };
              buttons.Children.Add(ok);
              Grid.SetRow(buttons, 2);
              layout.Children.Add(buttons);

              KeyEventHandler onKey = delegate(object sender, KeyEventArgs e)
              {
                  if (e.Key == Key.Enter) { TryConfirm(); e.Handled = true; }
                  else if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
              };
              box.KeyDown += onKey;
              plain.KeyDown += onKey;
              shell.Child = layout;
              Grid host = new Grid();
              host.Children.Add(shellShadow);
              host.Children.Add(shell);
              Content = host;
              Loaded += delegate { DisplayMotion.Reveal(this, layout); box.Focus(); };
          }

           void TryConfirm()
          {
              string error = validator == null ? null : validator(box.Password);
               if (!String.IsNullOrEmpty(error))
               {
                   revealer.Clear();
                   MessageBox.Show(this, error, "安全性", MessageBoxButton.OK, MessageBoxImage.Warning);
                   box.Focus();
                  return;
              }
               DialogResult = true;
           }

           internal void ClearInput()
           {
               if (revealer != null) revealer.Clear();
           }

          static SolidColorBrush MakeBrush(byte r, byte g, byte b)
          {
              SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
              brush.Freeze();
              return brush;
          }

          static Button MakeCloseButton()
          {
              Button button = new Button();
              button.Width = 34;
              button.Height = 30;
              button.Padding = new Thickness(0);
              button.Background = Brushes.Transparent;
              button.BorderThickness = new Thickness(0);
              button.FocusVisualStyle = null;
              button.Cursor = Cursors.Hand;
              button.Template = CodepassDialog.CreateBareButtonTemplate();
              Border border = new Border();
              border.CornerRadius = new CornerRadius(4);
              TextBlock text = new TextBlock();
              text.Text = "×";
              text.FontSize = 18;
              text.Foreground = MakeBrush(0x1C, 0x1C, 0x1C);
              text.HorizontalAlignment = HorizontalAlignment.Center;
              text.VerticalAlignment = VerticalAlignment.Center;
              border.Child = text;
              button.Content = border;
              return button;
          }
      }

      // 只在交互发生时创建短动画，不运行常驻刷新计时器。
      // 每次读取窗口所在显示器，跨屏或切换刷新率后下一次动画自动适配。
      static class DisplayMotion
      {
          [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
          struct MonitorInfo
          {
              public int Size;
              public int Left, Top, Right, Bottom;
              public int WorkLeft, WorkTop, WorkRight, WorkBottom;
              public int Flags;
              [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
              public string Device;
          }

          [DllImport("user32.dll")]
          static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
          [DllImport("user32.dll", CharSet = CharSet.Unicode)]
          static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
          [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
          static extern IntPtr CreateDC(string driver, string device, string output, IntPtr initData);
          [DllImport("gdi32.dll")]
          static extern int GetDeviceCaps(IntPtr dc, int index);
          [DllImport("gdi32.dll")]
          static extern bool DeleteDC(IntPtr dc);

          internal static int RefreshRate(Window window)
          {
              IntPtr dc = IntPtr.Zero;
              try
              {
                  IntPtr monitor = MonitorFromWindow(new WindowInteropHelper(window).Handle, 2);
                  MonitorInfo info = new MonitorInfo();
                  info.Size = Marshal.SizeOf(typeof(MonitorInfo));
                  if (GetMonitorInfo(monitor, ref info))
                  {
                      dc = CreateDC("DISPLAY", info.Device, null, IntPtr.Zero);
                      int rate = dc == IntPtr.Zero ? 0 : GetDeviceCaps(dc, 116); // VREFRESH
                      if (rate >= 24 && rate <= 1000) return rate;
                  }
              }
              catch { }
              finally { if (dc != IntPtr.Zero) DeleteDC(dc); }
              return 60; // 远程桌面/驱动未提供有效刷新率时回退。
          }

          internal static void Reveal(Window window, FrameworkElement content)
          {
              content.BeginAnimation(UIElement.OpacityProperty, null);
              content.Opacity = 1;
              if (!SystemParameters.ClientAreaAnimation) return;
              DoubleAnimation fade = new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(160));
              fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
              fade.FillBehavior = FillBehavior.Stop;
              Timeline.SetDesiredFrameRate(fade, RefreshRate(window));
              content.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
          }
      }
}
