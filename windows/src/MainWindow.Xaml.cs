// MainWindow 的运行时 XAML。保持为 C# 逐字字符串，以兼容当前无 csproj 的 C# 5 构建。

namespace Codepass
{
    sealed partial class MainWindow
    {
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
                     <Button x:Name='btnEmailTest' Style='{StaticResource SecondaryBtn}' Content='测试邮箱连接' Margin='0,0,8,0'/>
                     <Button x:Name='btnEmailRecheck' Style='{StaticResource SecondaryBtn}' Content='重新检查近 30 分钟' Margin='0,0,8,0'/>
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
                       <Button x:Name='btnDetect' Style='{StaticResource SecondaryBtn}' Content='检测本机地址' Margin='8,0,0,0'/>
                       <Button x:Name='btnTestPc' Style='{StaticResource SecondaryBtn}' Content='测试连通性' Margin='8,0,0,0'/>
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
                        <Button x:Name='btnTestNtfy' Style='{StaticResource SecondaryBtn}' Content='测试连通性' Margin='8,0,0,0'/>
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
                         <Button x:Name='btnEmailKeywordAdd' Style='{StaticResource SecondaryBtn}' Content='添加' Margin='8,0,0,0'/>
                         <Button x:Name='btnEmailKeywordDefault' Style='{StaticResource SecondaryBtn}' Content='恢复默认' Margin='8,0,0,0'/>
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
                     <Button x:Name='btnImport' Style='{StaticResource SecondaryBtn}' Content='导入设置' Margin='10,0,0,0'/>
                     <Button x:Name='btnExport' Style='{StaticResource SecondaryBtn}' Content='导出设置' Margin='10,0,0,0'/>
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
    }
}
