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
         SecretField fldToken, fldNtfyToken;
         CheckBox chkTip, chkStartup, chkAutoLock;
        ComboBox cmbPrivacy;
        Button btnTestPc, btnTestNtfy, btnLock;
        StackPanel listHost;
        TextBlock lblCount;
          Button navHistory, navGeneral, navSecurity, navAbout;
         Grid pageSet, pageLog, pageAbout;
         ScrollViewer settingsScroll;
          Border cardNetwork, cardGeneral, cardNotify, cardSecurity, cardMore;
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
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='Bd' Property='Background' Value='#4DF9F9F9'/>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
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
            <Button x:Name='navGeneral' Style='{StaticResource SideNavBtn}' Content='常规设置'/>
            <Button x:Name='navSecurity' Style='{StaticResource SideNavBtn}' Content='安全设置'/>
            <Button x:Name='navAbout' Style='{StaticResource SideNavBtn}' Content='关于'/>
          </StackPanel>
        </Border>

        <!-- 设置页 -->
        <Grid x:Name='pageSet' Grid.Column='1' Visibility='Collapsed'>
          <ScrollViewer x:Name='settingsScroll' VerticalScrollBarVisibility='Auto'>
            <StackPanel Margin='22,20,22,22'>

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
                               Content='随 Windows 启动 codepass（修改后自动保存）'
                               Foreground='#E4000000' FontSize='14' VerticalAlignment='Center'/>
                  </Grid>
                </StackPanel>
              </Border>

              <Border x:Name='cardNetwork' Style='{StaticResource Card}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource CardTitle}' Text='手机端连接设置'/>
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
                       <RowDefinition Height='42'/>
                     </Grid.RowDefinitions>

                    <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='电脑 IPv4 地址'/>
                    <StackPanel Grid.Row='0' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'>
                       <TextBox x:Name='txtPcIp' Style='{StaticResource Input}' Width='300'/>
                       <Button x:Name='btnDetect' Style='{StaticResource SecondaryBtn}' Content='检测本机地址' Margin='10,0,0,0'/>
                       <Button x:Name='btnTestPc' Style='{StaticResource SecondaryBtn}' Content='测试连通性' Margin='10,0,0,0'/>
                    </StackPanel>

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
                     <TextBlock Grid.Row='4' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='隐私保护'/>
                       <ComboBox x:Name='cmbPrivacy' Grid.Row='4' Grid.Column='1' Height='32' FontSize='14' Width='300' HorizontalAlignment='Left'
                                Style='{StaticResource PrivacyCombo}' ItemContainerStyle='{StaticResource PrivacyItem}'>
                       <ComboBoxItem Content='只提示收到，不显示内容'/>
                       <ComboBoxItem Content='显示内容，验证码替换为星号'/>
                       <ComboBoxItem Content='完整显示短信内容'/>
                     </ComboBox>
                   </Grid>
                   <TextBlock Style='{StaticResource CardTitle}' Text='公网中转（ntfy）' Margin='0,18,0,10'/>
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
                     <StackPanel Grid.Row='0' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'>
                        <TextBox x:Name='txtNtfyServer' Style='{StaticResource Input}' Width='300'/>
                       <Button x:Name='btnTestNtfy' Style='{StaticResource SecondaryBtn}' Content='测试连通性' Margin='10,0,0,0'/>
                     </StackPanel>

                     <TextBlock Grid.Row='1' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='主题名称'/>
                     <StackPanel Grid.Row='1' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'>
                        <TextBox x:Name='txtNtfyTopic' Style='{StaticResource Input}' Width='300'/>
                       <TextBlock Style='{StaticResource Hint}' Text='留空表示停用' Margin='10,0,0,0'/>
                     </StackPanel>

                     <TextBlock Grid.Row='2' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='访问令牌'/>
                      <StackPanel x:Name='ntfyTokenHost' Grid.Row='2' Grid.Column='1' Orientation='Horizontal' VerticalAlignment='Center'/>
                    </Grid>
                    <TextBlock Style='{StaticResource CardTitle}' Text='信息过滤（可选）' Margin='0,18,0,10'/>
                    <Grid>
                      <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='150'/>
                        <ColumnDefinition Width='*'/>
                      </Grid.ColumnDefinitions>
                      <Grid.RowDefinitions>
                        <RowDefinition Height='Auto'/>
                        <RowDefinition Height='Auto'/>
                      </Grid.RowDefinitions>

                      <TextBlock Grid.Row='0' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='过滤关键字'/>
                      <StackPanel Grid.Row='0' Grid.Column='1'>
                        <TextBox x:Name='txtFilterKeywords' Style='{StaticResource Input}' Height='32'
                                 VerticalContentAlignment='Center' Padding='6,0,7,0'
                                 AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                        <TextBlock Style='{StaticResource Hint}' Text='每行一个关键字，短信包含任一关键字时直接忽略' Margin='0,5,0,10'/>
                      </StackPanel>

                      <TextBlock Grid.Row='1' Grid.Column='0' Style='{StaticResource FieldLabel}' Text='过滤正则'/>
                      <StackPanel Grid.Row='1' Grid.Column='1'>
                        <TextBox x:Name='txtFilterRegex' Style='{StaticResource Input}' Height='32'
                                 VerticalContentAlignment='Center' Padding='6,0,7,0'
                                 AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                        <TextBlock Style='{StaticResource Hint}' Text='每行一条 .NET 正则表达式，任一匹配时直接忽略；留空表示不启用' Margin='0,5,0,0'/>
                      </StackPanel>
                    </Grid>
                  </StackPanel>
               </Border>

               <Border x:Name='cardNotify' Style='{StaticResource Card}' Visibility='Collapsed'/>

              <Border x:Name='cardSecurity' Style='{StaticResource Card}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource CardTitle}' Text='程序密码锁'/>
                  <CheckBox x:Name='chkAutoLock' Content='启用自动锁定（启动、重开窗口、最小化时需解锁）'
                            Foreground='#E4000000' FontSize='14'/>
                   <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,10,0,0'
                              Text='密码锁仅保护界面，不加密磁盘文件。自动锁定默认关闭；关闭时启动、重开窗口及最小化都不会要求解锁，可用“立即锁定”手动锁定。'/>
                   <Button x:Name='btnSecurity' Style='{StaticResource SecondaryBtn}' Content='管理密码锁' HorizontalAlignment='Left' Margin='0,14,0,0'/>
                   <Button x:Name='btnLock' Style='{StaticResource SecondaryBtn}' Content='立即锁定' HorizontalAlignment='Left' Margin='0,14,0,0'/>
                 </StackPanel>
               </Border>

              <Border x:Name='cardMore' Style='{StaticResource Card}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource CardTitle}' Text='配置管理'/>
                  <StackPanel Orientation='Horizontal'>
                    <Button x:Name='btnGen' Style='{StaticResource SecondaryBtn}' Content='复制手机端配置'/>
                    <Button x:Name='btnImport' Style='{StaticResource SecondaryBtn}' Content='导入设置' Margin='10,0,0,0'/>
                    <Button x:Name='btnExport' Style='{StaticResource SecondaryBtn}' Content='导出设置' Margin='10,0,0,0'/>
                  </StackPanel>
                  <TextBlock Style='{StaticResource Hint}' Text='设置修改后自动保存；端口 / ntfy 修改后请重启。' Margin='0,10,0,0'/>
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
                <RowDefinition Height='*'/>
              </Grid.RowDefinitions>

              <TextBlock Grid.Row='0' Style='{StaticResource CardTitle}' Text='验证码记录'/>

              <Grid Grid.Row='1' Height='30'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='140'/>
                  <ColumnDefinition Width='80'/>
                  <ColumnDefinition Width='120'/>
                  <ColumnDefinition Width='*'/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column='0' Style='{StaticResource TableHeader}' Text='时间'/>
                 <TextBlock Grid.Column='1' Style='{StaticResource TableHeader}' Text='发件人'/>
                <TextBlock Grid.Column='2' Style='{StaticResource TableHeader}' Text='验证码'/>
                <TextBlock Grid.Column='3' Style='{StaticResource TableHeader}' Text='原文'/>
              </Grid>

              <Border Grid.Row='2' BorderBrush='#0F000000' BorderThickness='0,1,0,0'>
                <ScrollViewer VerticalScrollBarVisibility='Auto' Margin='0,4,0,0'>
                  <StackPanel x:Name='listHost'/>
                </ScrollViewer>
              </Border>
            </Grid>
          </Border>

          <Border Grid.Row='1' Background='Transparent' BorderBrush='#0F000000' BorderThickness='0,1,0,0' Padding='22,12'>
            <StackPanel Orientation='Horizontal'>
               <Button x:Name='btnClear' Style='{StaticResource SecondaryBtn}' Content='清空记录' Margin='10,0,0,0'/>
              <TextBlock Style='{StaticResource Hint}' Text='单击验证码即可复制' Margin='14,0,0,0'/>
              <TextBlock x:Name='lblCount' Style='{StaticResource Hint}' Margin='14,0,0,0' Text='共 0 条'/>
            </StackPanel>
          </Border>
        </Grid>

          <Grid x:Name='pageAbout' Grid.Column='1' Visibility='Collapsed'>
            <ScrollViewer VerticalScrollBarVisibility='Auto'>
              <StackPanel Margin='22,20,22,22'>
                <Border Style='{StaticResource Card}'>
                  <StackPanel>
                    <TextBlock x:Name='lblAboutVersion' Foreground='#E4000000' FontSize='14' Text='codepass'/>
                    <StackPanel Orientation='Horizontal' Margin='0,16,0,0'>
                      <Button x:Name='btnGithub' Style='{StaticResource SecondaryBtn}' Content='打开 GitHub 主页'/>
                      <Button x:Name='btnUpdate' Style='{StaticResource SecondaryBtn}' Content='检查更新' Margin='10,0,0,0'/>
                    </StackPanel>
                    <TextBlock x:Name='lblUpdateStatus' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,10,0,0'
                               Text='尚未检查更新'/>
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
             MinWidth = 960;
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
             txtPcIp       = Find<TextBox>(root, "txtPcIp");
              txtNtfyServer = Find<TextBox>(root, "txtNtfyServer");
             txtNtfyTopic  = Find<TextBox>(root, "txtNtfyTopic");
            txtMin        = Find<TextBox>(root, "txtMin");
              txtMax        = Find<TextBox>(root, "txtMax");
              txtClear      = Find<TextBox>(root, "txtClear");
              txtFilterKeywords = Find<TextBox>(root, "txtFilterKeywords");
              txtFilterRegex = Find<TextBox>(root, "txtFilterRegex");
              chkTip        = Find<CheckBox>(root, "chkTip");
              chkStartup    = Find<CheckBox>(root, "chkStartup");
              chkAutoLock   = Find<CheckBox>(root, "chkAutoLock");
             cmbPrivacy    = Find<ComboBox>(root, "cmbPrivacy");
             btnTestPc     = Find<Button>(root, "btnTestPc");
             btnTestNtfy   = Find<Button>(root, "btnTestNtfy");
             btnLock       = Find<Button>(root, "btnLock");
             listHost      = Find<StackPanel>(root, "listHost");
            lblCount      = Find<TextBlock>(root, "lblCount");
            navHistory   = Find<Button>(root, "navHistory");
            navGeneral   = Find<Button>(root, "navGeneral");
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
             BuildSecretFields(root);
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
            Wire(root, "btnClear", delegate
            {
                 if (MessageBox.Show(this, "确定清空全部记录？", "确认",
                        MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                {
                    History.Clear();
                    RefreshHistory();
                }
            });

              if (navHistory != null) navHistory.Click += delegate { SelectSection("history"); };
              if (navGeneral != null) navGeneral.Click += delegate { SelectSection("general"); };
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
               TextBox[] boxes = new TextBox[] { txtPort, txtPcIp, txtMin, txtMax, txtClear,
                   txtNtfyServer, txtNtfyTopic, txtFilterKeywords, txtFilterRegex };
            foreach (TextBox tb in boxes)
            {
                if (tb == null) continue;
                tb.LostFocus += delegate { ApplySettings(); };
                 tb.KeyDown += delegate(object s, KeyEventArgs e)
                 {
                     // 多行过滤输入框用回车换行，失焦时自动保存；其他输入框回车立即保存。
                     if (e.Key == Key.Return && s != txtFilterKeywords && s != txtFilterRegex) ApplySettings();
                 };
            }
            if (chkTip != null) chkTip.Click += delegate { ApplySettings(); };
            if (chkStartup != null) chkStartup.Click += delegate { ApplySettings(); };
            if (chkAutoLock != null) chkAutoLock.Click += delegate { ApplySettings(); };
             if (cmbPrivacy != null) cmbPrivacy.SelectionChanged += delegate { ApplySettings(); };
              if (fldToken != null) fldToken.Committed += delegate { ApplySettings(); };
              if (fldNtfyToken != null) fldNtfyToken.Committed += delegate { ApplySettings(); };
             if (txtPcIp != null) txtPcIp.TextChanged += delegate { if (!loadingValues) autoPcIp = ""; };
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
              SetCardState(cardGeneral, general);
              SetCardState(cardNetwork, general);
              SetCardState(cardNotify, false);
              SetCardState(cardSecurity, security);
              SetCardState(cardMore, general);
              SetNavState(navHistory, history);
              SetNavState(navGeneral, general);
              SetNavState(navSecurity, security);
              SetNavState(navAbout, about);
              if (general || security)
              {
                  Border target = security ? cardSecurity : cardGeneral;
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
                 fldToken.Text = cfg.Token;
                 autoPcIp = cfg.PcIp.Length == 0 ? Program.DetectLocalIp() : "";
                 if (autoPcIp == "127.0.0.1") autoPcIp = "";
                 txtPcIp.Text = cfg.PcIp.Length > 0 ? cfg.PcIp : autoPcIp;
                  txtNtfyServer.Text = cfg.NtfyServer;
                  txtNtfyTopic.Text = cfg.NtfyTopic;
                  fldNtfyToken.Text = cfg.NtfyToken;
                  txtMin.Text = cfg.MinLen.ToString();
                  txtMax.Text = cfg.MaxLen.ToString();
                  txtClear.Text = cfg.AutoClearSeconds.ToString();
                  if (txtFilterKeywords != null) txtFilterKeywords.Text = cfg.FilterKeywords;
                  if (txtFilterRegex != null) txtFilterRegex.Text = cfg.FilterRegex;
                  chkTip.IsChecked = cfg.ShowTip;
                 if (chkStartup != null) chkStartup.IsChecked = cfg.StartWithWindows;
                 if (cmbPrivacy != null) cmbPrivacy.SelectedIndex = cfg.PrivacyMode < 0 || cfg.PrivacyMode > 2 ? 0 : cfg.PrivacyMode;
                 RefreshSecurityControls();
             }
             finally { loadingValues = false; }
         }

        /// <summary>输入完成即应用（失焦、回车、勾选切换）；无效输入提示并还原为已保存值。</summary>
         bool ApplySettings()
         {
              if (applying || loadingValues || txtPort == null) return !settingsRecoveryRequired;
             applying = true;
             try
            {
            int port, mn, mx, cl;
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
              if (!int.TryParse(txtClear.Text.Trim(), out cl) || cl < 0)
              {
                   ShowToast("自动清除需为不小于 0 的秒数，已还原");
                   txtClear.Text = cfg.AutoClearSeconds.ToString();
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
                      || ntfyUri.Scheme != Uri.UriSchemeHttps)
                  {
                      txtNtfyServer.Text = cfg.NtfyServer;
                      ShowToast("ntfy 服务器必须使用 HTTPS，已还原");
                     return false;
                 }
              }
             bool newShowTip = chkTip.IsChecked == true;
            bool newStartup = chkStartup != null && chkStartup.IsChecked == true;
            int newPrivacy = cmbPrivacy != null && cmbPrivacy.SelectedIndex >= 0 && cmbPrivacy.SelectedIndex <= 2 ? cmbPrivacy.SelectedIndex : 0;
            bool newAutoLock = cfg.LockEnabled && chkAutoLock != null && chkAutoLock.IsChecked == true;
            if (port == cfg.Port && newToken == cfg.Token && newPcIp == cfg.PcIp
                && mn == cfg.MinLen && mx == cfg.MaxLen && cl == cfg.AutoClearSeconds
                && newShowTip == cfg.ShowTip && newStartup == cfg.StartWithWindows
                  && newPrivacy == cfg.PrivacyMode && newNtfyServer == cfg.NtfyServer
                   && newNtfyTopic == cfg.NtfyTopic && newNtfyToken == cfg.NtfyToken
                  && newFilterKeywords == cfg.FilterKeywords && newFilterRegex == cfg.FilterRegex
                 && newAutoLock == cfg.AutoLockEnabled
                 && !Program.StartupSyncFailed)
                 {
                 if (settingsRecoveryRequired)
                     ShowToast("上次保存未完成，请修复配置文件后重试");
                 return !settingsRecoveryRequired;
             }

             Config previous = new Config();
             previous.CopyFrom(cfg);
             bool startupChanged = previous.StartWithWindows != newStartup;

             cfg.Port = port;
            cfg.Token = newToken;
            cfg.PcIp = newPcIp;
            cfg.MinLen = mn;
            cfg.MaxLen = mx;
            cfg.AutoClearSeconds = cl;
            cfg.ShowTip = newShowTip;
             cfg.StartWithWindows = newStartup;
            cfg.PrivacyMode = newPrivacy;
            cfg.NtfyServer = newNtfyServer;
              cfg.NtfyTopic = newNtfyTopic;
              cfg.NtfyToken = newNtfyToken;
              cfg.FilterKeywords = newFilterKeywords;
              cfg.FilterRegex = newFilterRegex;
             cfg.AutoLockEnabled = newAutoLock;

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
              }
              catch (Exception ex)
              {
                  bool restored = RestoreSettings(previous, startupAttempted);
                  settingsRecoveryRequired = !restored;
                  Log.Write("apply settings failed: " + ex.Message);
                  ShowToast(restored
                      ? "保存失败：" + ex.Message + "，已还原"
                      : "保存失败且还原失败，请检查配置文件和开机启动设置");
                  return false;
             }
             return true;
             }
            finally { applying = false; }
        }

         /// <summary>保存失败时恢复内存、界面、配置文件和已尝试同步的开机启动设置。</summary>
         bool RestoreSettings(Config previous, bool restoreStartup)
         {
             bool restored = true;
             cfg.CopyFrom(previous);
             try { cfg.Save(cfgPath); }
             catch (Exception ex)
             {
                 restored = false;
                 Log.Write("settings rollback save failed: " + ex.Message);
             }
             if (restoreStartup)
             {
                 try { Program.SetStartup(previous.StartWithWindows); }
                 catch (Exception ex)
                 {
                     restored = false;
                     Log.Write("settings rollback startup failed: " + ex.Message);
                 }
             }
             LoadValues();
             return restored;
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
                  || uri.Scheme != Uri.UriSchemeHttps)
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
                   "是否包含局域网令牌、ntfy 令牌和程序锁校验信息？\r\n\r\n包含敏感信息的文件不要发送给他人。",
                 "导出设置", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
             if (choice == MessageBoxResult.Cancel) return;
             try
             {
                 cfg.Save(dialog.FileName, choice == MessageBoxResult.Yes);
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
                         || importedNtfyServer.Scheme != Uri.UriSchemeHttps))
                    importedNtfyInvalid = true;
                string importedFilterError;
                bool importedFilterInvalid = !CodeExtractor.ValidateFilters(
                    imported.FilterKeywords, imported.FilterRegex, out importedFilterError);
                if (imported.Port < 1 || imported.Port > 65535 || imported.MinLen < 1 || imported.MinLen > 32
                    || imported.MaxLen < imported.MinLen || imported.MaxLen > 32
                    || imported.AutoClearSeconds < 0 || imported.PrivacyMode < 0 || imported.PrivacyMode > 2
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
                    imported.LockEnabled = cfg.LockEnabled;
                   imported.AutoLockEnabled = cfg.AutoLockEnabled;
                  imported.LockSalt = cfg.LockSalt;
                  imported.LockHash = cfg.LockHash;
               }
               if (!imported.WindowWidthIncluded) imported.WindowWidth = cfg.WindowWidth;
               if (!imported.WindowHeightIncluded) imported.WindowHeight = cfg.WindowHeight;
               cfg.CopyFrom(imported);
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
                    MessageBox.Show(this, "设置已导入并保存。" + (importedSecrets ? "" : "\r\n脱敏文件未包含令牌和密码锁，当前安全设置已保留。")
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
             fldToken = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
              fldNtfyToken = new SecretField(passwordStyle, inputStyle, buttonStyle, 300);
              AttachSecret(tokenHost, fldToken, "留空表示不验证");
               AttachSecret(ntfyHost, fldNtfyToken, "私有服务器可选");
         }

         static void AttachSecret(StackPanel host, SecretField field, string hint)
         {
             if (host == null) return;
             field.Attach(host);
             TextBlock label = new TextBlock();
             label.Text = hint;
             label.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
             label.FontSize = 12.5;
             label.Margin = new Thickness(10, 0, 0, 0);
             label.VerticalAlignment = VerticalAlignment.Center;
             host.Children.Add(label);
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
            try { System.Windows.Clipboard.SetText(text); ok = true; } catch { }
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

        public void RefreshHistory()
        {
            if (listHost == null) return;
            listHost.Children.Clear();
            selectedRow = null;
            foreach (HistoryItem it in History.Snapshot())
                listHost.Children.Add(BuildRow(it));
            UpdateCount();
        }

        Border BuildRow(HistoryItem it)
        {
            Grid g = new Grid();
            AddCol(g, 140, false);
            AddCol(g, 80, false);
            AddCol(g, 120, false);
            AddCol(g, 0, true);

            g.Children.Add(Cell(it.Time, 0, FgBody, false));
            g.Children.Add(Cell(it.Source, 1, FgBody, false));
            TextBlock codeCell = Cell(it.Code, 2, AccentTx, true);
            g.Children.Add(codeCell);
             TextBlock rawCell = Cell(it.Raw, 3, FgFaint, false);
             rawCell.ToolTip = String.IsNullOrEmpty(it.Raw) ? null : it.Raw;
             g.Children.Add(rawCell);

            Border row = new Border();
            row.Height = 40;
            row.Margin = new Thickness(0, 2, 0, 2);
            row.CornerRadius = new CornerRadius(4);
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;
            row.Child = g;
            row.ToolTip = "单击验证码可复制";

            string code = it.Code;
            codeCell.Cursor = Cursors.Hand;
            codeCell.ToolTip = "单击复制验证码";
            codeCell.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (string.IsNullOrEmpty(code)) return;
                // 只复制验证码；点击“原文”等区域不再占用剪贴板（避免点击卡顿）。
                try
                {
                    System.Windows.Clipboard.SetText(code);
                    ShowToast("已复制验证码 " + code);
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
            if (star) c.Width = new GridLength(1, GridUnitType.Star);
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
            if (bold) t.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(t, col);
            return t;
        }

        public void OnNewRecord(HistoryItem it)
        {
            if (listHost == null) return;
            listHost.Children.Insert(0, BuildRow(it));
            while (listHost.Children.Count > History.Max)
            {
                UIElement last = listHost.Children[listHost.Children.Count - 1];
                listHost.Children.RemoveAt(listHost.Children.Count - 1);
                if (last == selectedRow) selectedRow = null;
            }
            UpdateCount();
        }

        void UpdateCount()
        {
            if (lblCount != null)
                lblCount.Text = "共 " + (listHost == null ? 0 : listHost.Children.Count)
                              + " 条 · 最多保留 " + History.Max + " 条";
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
            Updater.RunFullyAutomatic(
                delegate(string s) { SetUpdateStatus(s); },
                delegate
                {
                    // 替换完成，即将退出并自动重启
                    updateBusy = false;
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                         bool exited = false;
                         try { exited = Program.QuitApp(); } catch { }
                         if (!exited)
                         {
                              if (btnUpdate != null) btnUpdate.IsEnabled = true;
                             SetUpdateStatus("更新已安装，请修复设置后关闭程序以完成重启");
                         }
                    }));
                },
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
              if (!ApplySettings())
              {
                  allowClose = false;
                  e.Cancel = true;
                  return;
              }
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
