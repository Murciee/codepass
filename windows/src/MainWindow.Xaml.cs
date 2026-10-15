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
      xmlns:local='clr-namespace:Codepass;assembly=codepass'
      xmlns:sys='clr-namespace:System;assembly=mscorlib'
      Background='#F3F3F3'>
  <Grid.Resources>
    <sys:Double x:Key='SettingsInputWidth'>180</sys:Double>
    <sys:Double x:Key='SettingsGroupInputWidth'>284</sys:Double>

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
            <!-- 文字在左、开关推到整行最右：宽行里开关成一条右对齐竖线。 -->
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <ContentPresenter Grid.Column='0' VerticalAlignment='Center'/>
              <Grid Grid.Column='1' Width='40' Height='20' VerticalAlignment='Center' Margin='10,0,0,0'>
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
            </Grid>
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

    <!-- Fluent / Windows 11 SettingsCard：一条设置一张卡片，标题与描述在左，控件贴右。
         内边距 20 与行式卡片 Card 完全一致，保证所有文字同一左线、所有控件同一右线。 -->
    <Style x:Key='SettingsCard' TargetType='Border'>
      <Setter Property='Background' Value='#B3FFFFFF'/>
      <Setter Property='BorderBrush' Value='#0F000000'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='4'/>
      <Setter Property='Padding' Value='20,16,20,16'/>
      <Setter Property='Margin' Value='0,0,0,4'/>
      <Setter Property='MinHeight' Value='64'/>
      <Style.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
          <Setter Property='Background' Value='#E6FFFFFF'/>
          <Setter Property='BorderBrush' Value='#1F000000'/>
        </Trigger>
      </Style.Triggers>
    </Style>

    <Style x:Key='SettingsCardHeader' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='TextWrapping' Value='Wrap'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
    </Style>

    <Style x:Key='SettingsCardDescription' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Foreground' Value='#6B7280'/>
      <Setter Property='TextWrapping' Value='Wrap'/>
      <Setter Property='Margin' Value='0,3,0,0'/>
      <!-- 描述为空时不占位：否则卡片左侧会被空行推高，文字看着不居中。 -->
      <Style.Triggers>
        <Trigger Property='Text' Value=''>
          <Setter Property='Visibility' Value='Collapsed'/>
          <Setter Property='Margin' Value='0'/>
        </Trigger>
      </Style.Triggers>
    </Style>

    <!-- 输入框一体化容器：输入框 + 内嵌后缀文字 / 内嵌图标 / 右侧动作段共用一条边框。 -->
    <Style x:Key='InputAddon' TargetType='Border'>
      <Setter Property='Background' Value='#B3FFFFFF'/>
      <Setter Property='BorderBrush' Value='{StaticResource TextControlElevationBorder}'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='4'/>
      <Setter Property='Height' Value='32'/>
      <Style.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
          <Setter Property='Background' Value='#80F9F9F9'/>
        </Trigger>
        <Trigger Property='IsKeyboardFocusWithin' Value='True'>
          <Setter Property='Background' Value='#FFFFFF'/>
          <Setter Property='BorderBrush' Value='#0078D4'/>
        </Trigger>
      </Style.Triggers>
    </Style>

    <!-- 一体化容器里的无边框输入框（边框由 InputAddon 提供）。 -->
    <Style x:Key='InnerInput' TargetType='TextBox'>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='Padding' Value='7,0,6,0'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='CaretBrush' Value='#E4000000'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='TextBox'>
            <Grid>
              <Border x:Name='Bd' Background='Transparent' BorderThickness='0'/>
              <Border x:Name='Underline' Height='2' VerticalAlignment='Bottom' Margin='1,0,1,0'
                      Background='#00000000' CornerRadius='0,0,3,3' RenderTransformOrigin='0,0.5'>
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
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='InnerPassword' TargetType='PasswordBox'>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='Padding' Value='7,0,6,0'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='CaretBrush' Value='#E4000000'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='PasswordBox'>
            <Grid>
              <Border x:Name='Bd' Background='Transparent' BorderThickness='0'/>
              <ScrollViewer x:Name='PART_ContentHost' Focusable='False' Margin='{TemplateBinding Padding}'
                            VerticalAlignment='Center'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- 输入框内的后缀文字：秒 / 字符 等，贴右边缘，不参与点击。 -->
    <Style x:Key='InputSuffix' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='12.5'/>
      <Setter Property='Foreground' Value='#6B7280'/>
      <Setter Property='VerticalAlignment' Value='Center'/>
      <Setter Property='Margin' Value='4,0,9,0'/>
      <Setter Property='IsHitTestVisible' Value='False'/>
    </Style>

    <!-- 输入框内右边缘的图标按钮（眼睛等）：无边框、悬停轻微底色。 -->
    <Style x:Key='InlineIconBtn' TargetType='Button'>
      <Setter Property='FontFamily' Value='Segoe Fluent Icons, Segoe MDL2 Assets'/>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='Foreground' Value='#6B7280'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Width' Value='30'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='Bd' Background='Transparent' CornerRadius='3' Margin='2'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#14000000'/>
                <Setter Property='Foreground' Value='#E4000000'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#24000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- 输入框一体化容器右侧的动作段（如检测本机地址）：整段可点，左侧一条分隔线。 -->
    <Style x:Key='InputSegmentBtn' TargetType='Button'>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Padding' Value='12,0,12,0'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='Bd' Background='Transparent' BorderBrush='#0F000000' BorderThickness='1,0,0,0'>
              <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#14000000'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Bd' Property='Background' Value='#24000000'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- 分组容器：不再是卡片本身，只负责把一组 SettingsCard 收在一起。 -->
    <Style x:Key='SettingsGroup' TargetType='Border'>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='Padding' Value='0'/>
      <Setter Property='Margin' Value='0,0,0,18'/>
    </Style>

    <!-- 分组标题与分组说明：左边的 20 与卡片内边距一致，使整页文字落在同一条左线上。 -->
    <Style x:Key='SettingsGroupTitle' TargetType='TextBlock'>
      <Setter Property='FontSize' Value='14'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
      <Setter Property='Foreground' Value='#E4000000'/>
      <Setter Property='Margin' Value='20,0,0,8'/>
    </Style>

    <Style x:Key='SettingsGroupDescription' BasedOn='{StaticResource SettingsCardDescription}' TargetType='TextBlock'>
      <Setter Property='Margin' Value='20,0,0,10'/>
    </Style>

    <!-- 可点/可跳转的 SettingsCard：整行可点，右侧带 chevron。 -->
    <Style x:Key='SettingsCardLink' TargetType='Button'>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Grid>
              <Border x:Name='Fill' Background='#B3FFFFFF' BorderBrush='#0F000000' BorderThickness='1' CornerRadius='4'/>
              <ContentPresenter Margin='16,14,34,14' VerticalAlignment='Center' HorizontalAlignment='Stretch'/>
              <TextBlock x:Name='Chevron' Text='&#xE76C;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='12'
                         Foreground='#9E000000' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,14,0'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Fill' Property='Background' Value='#E6FFFFFF'/>
                <Setter TargetName='Fill' Property='BorderBrush' Value='#1F000000'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Fill' Property='Background' Value='#CCF9F9F9'/>
                <Setter TargetName='Chevron' Property='Foreground' Value='#E4000000'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='Fill' Property='Background' Value='#4DF9F9F9'/>
                <Setter TargetName='Chevron' Property='Visibility' Value='Collapsed'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <!-- 描述行里的文字动作（如测试连通性）：无边框、强调色、前置小图标，与描述同一条基线。 -->
    <Style x:Key='FieldLinkBtn' TargetType='Button'>
      <Setter Property='Foreground' Value='#0078D4'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Padding' Value='0'/>
      <Setter Property='FocusVisualStyle' Value='{StaticResource KeyboardFocus}'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='Bd' Background='Transparent' Padding='1,0,1,0'>
              <StackPanel Orientation='Horizontal'>
                <TextBlock Text='&#xE71B;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='12'
                           VerticalAlignment='Center' Margin='0,0,4,0'/>
                <ContentPresenter VerticalAlignment='Center'/>
              </StackPanel>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter Property='Foreground' Value='#005A9E'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter Property='Foreground' Value='#003D69'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter Property='Foreground' Value='#5C000000'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
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

    <!-- 表单标签：单元格位置由 InitializeResponsiveForms 统一设置，
         这里只能设字体与颜色，禁止再设 Height / Padding / Margin，
         否则该卡片的标签会整体偏离同一行的输入框。 -->
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
              <Border x:Name='Fill' Background='#0067C0' CornerRadius='4'/>
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
              <Border x:Name='Fill' Background='Transparent' CornerRadius='4'/>
              <Border x:Name='Stroke' BorderBrush='#E5E5E5'
                      BorderThickness='1' CornerRadius='4'/>
              <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Fill' Property='Background' Value='#F5F5F5'/>
              </Trigger>
              <Trigger Property='IsPressed' Value='True'>
                <Setter TargetName='Fill' Property='Background' Value='#EBEBEB'/>
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
      <Setter Property='Foreground' Value='#6B7280'/>
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

  <!-- 圆角外壳 (窗口无边框, 外层留白放阴影) -->
  <Border x:Name='shell' Margin='6' CornerRadius='8' Background='#F3F3F3'
          BorderBrush='#0F000000' BorderThickness='1'>

    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height='58'/>
        <RowDefinition Height='*'/>
        <RowDefinition Height='Auto'/>
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
            <Button x:Name='btnMin' Style='{StaticResource CaptionBtn}' Content='&#xE921;' AutomationProperties.Name='最小化'/>
            <Button x:Name='btnMax' Style='{StaticResource CaptionBtn}' Content='&#xE922;' AutomationProperties.Name='最大化或还原'/>
            <Button x:Name='btnClose' Style='{StaticResource CloseBtn}' Content='&#xE8BB;' AutomationProperties.Name='隐藏到托盘'/>
          </StackPanel>
        </Grid>
      </Border>

      <!-- 内容区 -->
      <Grid x:Name='businessContent' Grid.Row='1'>
        <Grid.ColumnDefinitions>
          <ColumnDefinition x:Name='navWidth' Width='178'/>
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
            <StackPanel MaxWidth='560' HorizontalAlignment='Center' Margin='0,20,0,22'>

              <Border Background='#FFF4CE' CornerRadius='6' Padding='10' Margin='0,0,0,12' x:Name='storageErrorBanner' Visibility='Collapsed'>
                <StackPanel>
                  <TextBlock x:Name='lblStorageError' TextWrapping='Wrap' Foreground='#8A4B08' FontSize='12'/>
                  <Button x:Name='btnStorageRetry' Style='{StaticResource SecondaryBtn}' Content='重试保存与邮件检查' HorizontalAlignment='Left' Margin='0,8,0,0'/>
                </StackPanel>
              </Border>

              <!-- 分组：电脑端接收 -->
              <Border x:Name='cardGeneral' Style='{StaticResource SettingsGroup}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='电脑端接收'/>
                  <TextBlock Style='{StaticResource SettingsGroupDescription}' Text='局域网接收需设置访问令牌；未设置时只监听本机，改端口或令牌后重启。'/>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='监听端口'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='1～65535'/>
                      </StackPanel>
                      <TextBox x:Name='txtPort' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}' Width='72' HorizontalAlignment='Right'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='访问令牌'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='局域网请求的校验口令；留空表示不校验'/>
                      </StackPanel>
                      <StackPanel x:Name='tokenHost' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='静默启动'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='随 Windows 静默启动 codepass（驻留托盘）'/>
                      </StackPanel>
                      <CheckBox x:Name='chkStartup' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center' AutomationProperties.Name='静默启动'/>
                    </Grid>
                  </Border>
                </StackPanel>
              </Border>

              <!-- 分组：手机连接与验证码处理 -->
              <Border x:Name='cardNetwork' Style='{StaticResource SettingsGroup}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='手机连接'/>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='电脑 IPv4 地址'/>
                        <StackPanel Orientation='Horizontal' Margin='0,4,0,0'>
                          <Button x:Name='btnDetect' Style='{StaticResource FieldLinkBtn}' Content='检测本机地址' AutomationProperties.Name='检测本机地址'/>
                          <Button x:Name='btnTestPc' Style='{StaticResource FieldLinkBtn}' Content='测试连通性' Margin='14,0,0,0'/>
                        </StackPanel>
                        <TextBlock x:Name='lblPcStatus' Style='{StaticResource SettingsCardDescription}' Margin='0,4,0,0'/>
                      </StackPanel>
                      <TextBox x:Name='txtPcIp' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}' Width='132' HorizontalAlignment='Right'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='验证码长度'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='1～32；不在此范围的数字不算验证码'/>
                      </StackPanel>
                      <Grid x:Name='codeLengthRow' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right'>
                        <StackPanel Orientation='Horizontal'>
                          <TextBox x:Name='txtMin' Style='{StaticResource Input}' Width='46' HorizontalAlignment='Center' AutomationProperties.Name='最短验证码长度'/>
                          <TextBlock Text='~' Foreground='#6B7280' FontSize='13' VerticalAlignment='Center' Margin='8,0,8,0'/>
                          <TextBox x:Name='txtMax' Style='{StaticResource Input}' Width='46' HorizontalAlignment='Center' AutomationProperties.Name='最长验证码长度'/>
                        </StackPanel>
                      </Grid>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='自动清除剪贴板'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='0 表示不清除'/>
                      </StackPanel>
                      <Border Grid.Row='0' Grid.Column='1' Style='{StaticResource InputAddon}' Width='92' HorizontalAlignment='Right'>
                        <Grid>
                          <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                          <TextBox x:Name='txtClear' Grid.Column='0' Style='{StaticResource InnerInput}'/>
                          <TextBlock Grid.Column='1' Style='{StaticResource InputSuffix}' Text='秒'/>
                        </Grid>
                      </Border>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='桌面通知'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='收到验证码后显示通知'/>
                      </StackPanel>
                      <CheckBox x:Name='chkTip' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center' AutomationProperties.Name='桌面通知'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='手机脚本接入'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='复制配置给手机脚本，或查看 Webhook 接入说明'/>
                      </StackPanel>
                      <StackPanel Grid.Row='0' Grid.Column='1' Orientation='Horizontal' HorizontalAlignment='Right' VerticalAlignment='Center'>
                        <Button x:Name='btnGen' Style='{StaticResource SecondaryBtn}' Content='复制配置'/>
                        <Button x:Name='btnWebhook' Style='{StaticResource SecondaryBtn}' Content='Webhook 接入' Margin='8,0,0,0'/>
                      </StackPanel>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='测试信息'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='模拟一条短信，验证记录、复制与通知'/>
                      </StackPanel>
                      <Button x:Name='btnTestSms' Grid.Row='0' Grid.Column='1' Style='{StaticResource SecondaryBtn}' Content='模拟收到短信' HorizontalAlignment='Right' VerticalAlignment='Center'/>
                    </Grid>
                  </Border>
                </StackPanel>
              </Border>

              <!-- 分组：公网中转 -->
              <Border x:Name='cardNtfy' Style='{StaticResource SettingsGroup}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='公网中转'/>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='服务器地址'/>
                        <StackPanel Orientation='Horizontal' Margin='0,3,0,0'>
                          <TextBlock Style='{StaticResource SettingsCardDescription}' Margin='0' VerticalAlignment='Center' Text='必须是 HTTPS 地址 ·'/>
                          <Button x:Name='btnTestNtfy' Style='{StaticResource FieldLinkBtn}' Content='测试连通性' Margin='3,0,0,0'/>
                        </StackPanel>
                      </StackPanel>
                      <TextBox x:Name='txtNtfyServer' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}' Width='{DynamicResource SettingsInputWidth}' HorizontalAlignment='Right'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='主题名称'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='留空表示停用'/>
                      </StackPanel>
                      <TextBox x:Name='txtNtfyTopic' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}' Width='{DynamicResource SettingsInputWidth}' HorizontalAlignment='Right'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='访问令牌'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='ntfy 服务的令牌；留空表示不需要'/>
                      </StackPanel>
                      <StackPanel x:Name='ntfyTokenHost' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center'/>
                    </Grid>
                  </Border>
                </StackPanel>
              </Border>

              <!-- 分组：短信过滤规则 -->
              <Border x:Name='cardSmsFilters' Style='{StaticResource SettingsGroup}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='短信过滤'/>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCardStacked'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='接收关键字'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='短信包含任一关键字才会收取；留空表示全部收取；点 × 移除。'/>
                        <WrapPanel x:Name='acceptKeywordTags' Margin='0,8,0,0'/>
                      </StackPanel>
                      <Grid Tag='FieldActions' Grid.Row='1' Grid.Column='0' HorizontalAlignment='Right'>
                        <Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                        <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                        <TextBox x:Name='txtAcceptKeywordAdd' Style='{StaticResource Input}' Width='160' Visibility='Collapsed' ToolTip='输入关键字，按回车添加；支持粘贴多行'/>
                        <WrapPanel Grid.Column='1'>
                          <Button x:Name='btnAcceptKeywordAdd' Style='{StaticResource SecondaryBtn}' Content='添加'/>
                          <Button x:Name='btnAcceptKeywordDefault' Style='{StaticResource SecondaryBtn}' Content='恢复默认' Margin='8,0,0,0'/>
                        </WrapPanel>
                      </Grid>
                      <StackPanel Grid.Row='2' Grid.Column='0' Margin='0,6,0,0'>
                        <Expander Header='批量编辑（每行一个关键字）' FontSize='12.5'>
                          <TextBox x:Name='txtAcceptKeywords' Style='{StaticResource Input}' Margin='0,6,0,0'
                                   Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                                   AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                        </Expander>
                      </StackPanel>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='短信长度上限'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='1～10000；推荐 500。超过上限整条忽略，不记入历史、不提示、不复制，空格和换行也计入'/>
                      </StackPanel>
                      <Border Grid.Row='0' Grid.Column='1' Style='{StaticResource InputAddon}' Width='118' HorizontalAlignment='Right'>
                        <Grid>
                          <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                          <TextBox x:Name='txtSmsMaxLength' Grid.Column='0' Style='{StaticResource InnerInput}'/>
                          <TextBlock Grid.Column='1' Style='{StaticResource InputSuffix}' Text='字符'/>
                        </Grid>
                      </Border>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCardStacked'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='过滤关键字'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='每行一个关键字，短信包含任一关键字时直接忽略；留空表示不启用'/>
                      </StackPanel>
                      <TextBox x:Name='txtFilterKeywords' Grid.Row='1' Grid.Column='0' Style='{StaticResource Input}'
                               Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                               AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCardStacked'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='过滤正则'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='每行一条 .NET 正则表达式，任一匹配时直接忽略；留空表示不启用'/>
                      </StackPanel>
                      <TextBox x:Name='txtFilterRegex' Grid.Row='1' Grid.Column='0' Style='{StaticResource Input}'
                               Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                               AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                    </Grid>
                  </Border>
                </StackPanel>
              </Border>

              <!-- 分组：邮箱连接 -->
              <Border x:Name='cardEmail' Style='{StaticResource SettingsGroup}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='邮箱连接'/>
                  <TextBlock Style='{StaticResource SettingsGroupDescription}' Text='连接需测试并保存；接收规则自动保存。'/>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='启用邮箱接收'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='接收邮箱里的验证码邮件'/>
                      </StackPanel>
                      <CheckBox x:Name='chkEmail' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center' AutomationProperties.Name='启用邮箱接收'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='邮箱服务'/>
                        <TextBlock x:Name='emailProviderHint' Style='{StaticResource SettingsCardDescription}'/>
                      </StackPanel>
                      <ComboBox x:Name='cmbEmailProvider' Grid.Row='0' Grid.Column='1' Height='32' FontSize='14' Width='130' HorizontalAlignment='Right' VerticalAlignment='Center'
                                Style='{StaticResource PrivacyCombo}' ItemContainerStyle='{StaticResource PrivacyItem}'>
                        <ComboBoxItem Content='QQ 邮箱'/>
                        <ComboBoxItem Content='163 邮箱'/>
                        <ComboBoxItem Content='126 邮箱'/>
                        <ComboBoxItem Content='Gmail'/>
                        <ComboBoxItem Content='Outlook'/>
                        <ComboBoxItem Content='自定义'/>
                      </ComboBox>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='服务器地址'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='自定义服务时填写 IMAP 服务器；SSL 加密，端口 993'/>
                      </StackPanel>
                      <TextBox x:Name='txtEmailServer' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}' Width='{DynamicResource SettingsInputWidth}' HorizontalAlignment='Right' AutomationProperties.Name='IMAP 服务器地址'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='邮箱账号'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='完整邮箱地址'/>
                      </StackPanel>
                      <TextBox x:Name='txtEmailAccount' Grid.Row='0' Grid.Column='1' Style='{StaticResource Input}' Width='{DynamicResource SettingsInputWidth}' HorizontalAlignment='Right'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='授权码 / 应用密码'/>
                        <TextBlock x:Name='emailAuthHint' Style='{StaticResource SettingsCardDescription}'/>
                      </StackPanel>
                      <StackPanel x:Name='emailAuthHost' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='测试信息'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='模拟一封验证码邮件，验证记录、复制与通知'/>
                      </StackPanel>
                      <Button x:Name='btnTestEmail' Grid.Row='0' Grid.Column='1' Style='{StaticResource SecondaryBtn}' Content='模拟收到邮件' HorizontalAlignment='Right' VerticalAlignment='Center'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='连接检查'/>
                        <TextBlock x:Name='lblEmailTestResult' Style='{StaticResource SettingsCardDescription}'/>
                      </StackPanel>
                      <StackPanel Grid.Row='0' Grid.Column='1' Orientation='Horizontal' HorizontalAlignment='Right' VerticalAlignment='Center'>
                        <Button x:Name='btnEmailTest' Style='{StaticResource SecondaryBtn}' Content='测试邮箱连接'/>
                        <Button x:Name='btnEmailSave' Style='{StaticResource PrimaryBtn}' Content='保存连接' Margin='8,0,0,0'/>
                      </StackPanel>
                    </Grid>
                  </Border>

                  <Button x:Name='btnEmailAuthHelp' Style='{StaticResource SettingsCardLink}' Content='打开 Google 应用密码设置' Margin='0,0,0,4' Visibility='Collapsed'/>
                  <Button x:Name='btnEmailGuide' Style='{StaticResource SettingsCardLink}' Content='Gmail 设置教程' Visibility='Collapsed'/>
                </StackPanel>
              </Border>

              <!-- 分组：邮件接收规则 -->
              <Border x:Name='cardEmailFilters' Style='{StaticResource SettingsGroup}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='接收规则'/>
                  <TextBlock Style='{StaticResource SettingsGroupDescription}' Text='匹配任一关键词；留空不接收。'/>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCardStacked'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='主题关键词'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='邮箱主题包含任一关键词才接收；点 × 移除。'/>
                        <WrapPanel x:Name='emailKeywordTags' Margin='0,8,0,0'/>
                      </StackPanel>
                      <Grid Tag='FieldActions' Grid.Row='1' Grid.Column='0' HorizontalAlignment='Right'>
                        <Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                        <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                        <TextBox x:Name='txtEmailKeywordAdd' Style='{StaticResource Input}' Width='160' Visibility='Collapsed' ToolTip='输入关键词，按回车添加；支持粘贴多行'/>
                        <WrapPanel Grid.Column='1'>
                          <Button x:Name='btnEmailKeywordAdd' Style='{StaticResource SecondaryBtn}' Content='添加'/>
                          <Button x:Name='btnEmailKeywordDefault' Style='{StaticResource SecondaryBtn}' Content='恢复默认' Margin='8,0,0,0'/>
                        </WrapPanel>
                      </Grid>
                      <StackPanel Grid.Row='2' Grid.Column='0' Margin='0,6,0,0'>
                        <Expander Header='批量编辑（每行一个关键词）' FontSize='12.5'>
                          <TextBox x:Name='txtEmailKeywords' Style='{StaticResource Input}' Margin='0,6,0,0'
                                   Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                                   AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                        </Expander>
                        <Expander Header='测试主题匹配' FontSize='12.5' Margin='0,6,0,0'>
                          <StackPanel>
                            <TextBox x:Name='txtEmailSubjectPreview' Style='{StaticResource Input}' Margin='0,6,0,0' ToolTip='输入示例邮件主题，只在本机预览规则'/>
                            <TextBlock x:Name='lblEmailPreviewResult' Style='{StaticResource Hint}' Margin='0,5,0,0' Text='输入主题查看是否会被接收'/>
                          </StackPanel>
                        </Expander>
                      </StackPanel>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCardStacked'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='发件人白名单'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='每行一个邮箱或域名；留空不限'/>
                      </StackPanel>
                      <TextBox x:Name='txtEmailSenders' Grid.Row='1' Grid.Column='0' Style='{StaticResource Input}'
                               Height='Auto' MinHeight='32' MaxHeight='96' Padding='6,3,7,3'
                               AcceptsReturn='True' TextWrapping='Wrap' VerticalScrollBarVisibility='Auto'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='到达提示'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='收到验证码邮件后弹出桌面通知'/>
                      </StackPanel>
                      <CheckBox x:Name='chkEmailNotify' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center' AutomationProperties.Name='到达提示'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='自动复制'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='识别到验证码后直接写入剪贴板'/>
                      </StackPanel>
                      <CheckBox x:Name='chkEmailAutoCopy' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center' AutomationProperties.Name='自动复制'/>
                    </Grid>
                  </Border>
                </StackPanel>
              </Border>

              <!-- 分组：邮箱高级设置 -->
              <Border x:Name='cardQqPopup' Style='{StaticResource SettingsGroup}'>
                <StackPanel x:Name='emailAdvanced'>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='高级设置'/>
                  <TextBlock Style='{StaticResource SettingsGroupDescription}' Text='默认只存发件人与验证码，记录加密保存。'/>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='检查间隔'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='10～600；支持 IDLE 时实时接收，其他目录定时检查'/>
                      </StackPanel>
                      <Border Grid.Row='0' Grid.Column='1' Style='{StaticResource InputAddon}' Width='92' HorizontalAlignment='Right'>
                        <Grid>
                          <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                          <TextBox x:Name='txtEmailPoll' Grid.Column='0' Style='{StaticResource InnerInput}'/>
                          <TextBlock Grid.Column='1' Style='{StaticResource InputSuffix}' Text='秒'/>
                        </Grid>
                      </Border>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='同时检查垃圾 / 广告文件夹'/>
                      </StackPanel>
                      <CheckBox x:Name='chkEmailJunk' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center' AutomationProperties.Name='同时检查垃圾 / 广告文件夹'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='在记录中保存邮件正文'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='正文最多保存 20000 字符；加密记录仅限当前 Windows 账户读取'/>
                      </StackPanel>
                      <CheckBox x:Name='chkEmailSaveBody' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center' AutomationProperties.Name='在记录中保存邮件正文'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='QQ 弹窗联动'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='仅用 QQ 提醒标题触发邮箱检查，不读取聊天内容'/>
                        <TextBlock x:Name='lblQqStatus' Style='{StaticResource SettingsCardDescription}'/>
                      </StackPanel>
                      <CheckBox x:Name='chkQqPopup' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center'
                                AutomationProperties.Name='QQ 弹窗联动' ToolTip='仅用 QQ 提醒标题触发邮箱检查，不读取聊天内容。' ToolTipService.ShowOnDisabled='True'/>
                    </Grid>
                  </Border>
                </StackPanel>
              </Border>

              <!-- 分组：通知隐私 -->
              <Border x:Name='cardNotify' Style='{StaticResource SettingsGroup}' Visibility='Collapsed'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='通知隐私'/>
                  <TextBlock Style='{StaticResource SettingsGroupDescription}' Text='两种通道分别设置，默认只提示收到；不影响剪贴板与记录原文。'/>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='短信通知内容'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='短信桌面通知里显示到什么程度'/>
                      </StackPanel>
                      <ComboBox x:Name='cmbPrivacy' Grid.Row='0' Grid.Column='1' Height='32' FontSize='14' Width='{DynamicResource SettingsInputWidth}' HorizontalAlignment='Right' VerticalAlignment='Center'
                                Style='{StaticResource PrivacyCombo}' ItemContainerStyle='{StaticResource PrivacyItem}'>
                        <ComboBoxItem Content='只提示收到'/>
                        <ComboBoxItem Content='验证码打码'/>
                        <ComboBoxItem Content='完整显示'/>
                      </ComboBox>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='邮件通知内容'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='邮件桌面通知里显示到什么程度'/>
                      </StackPanel>
                      <ComboBox x:Name='cmbEmailPrivacy' Grid.Row='0' Grid.Column='1' Height='32' FontSize='14' Width='{DynamicResource SettingsInputWidth}' HorizontalAlignment='Right' VerticalAlignment='Center'
                                Style='{StaticResource PrivacyCombo}' ItemContainerStyle='{StaticResource PrivacyItem}'>
                        <ComboBoxItem Content='只提示收到'/>
                        <ComboBoxItem Content='验证码打码'/>
                        <ComboBoxItem Content='完整显示'/>
                      </ComboBox>
                    </Grid>
                  </Border>
                </StackPanel>
              </Border>

              <!-- 分组：程序密码锁 -->
              <Border x:Name='cardSecurity' Style='{StaticResource SettingsGroup}'>
                <StackPanel>
                  <TextBlock Style='{StaticResource SettingsGroupTitle}' Text='密码锁'/>

                  <Border Style='{StaticResource SettingsCard}' Tag='SettingsToggle'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='启用自动锁定'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='启动、重开窗口、最小化时需解锁'/>
                      </StackPanel>
                      <CheckBox x:Name='chkAutoLock' Grid.Row='0' Grid.Column='1' HorizontalAlignment='Right' VerticalAlignment='Center'
                                AutomationProperties.Name='启用自动锁定'/>
                    </Grid>
                  </Border>

                  <Border Style='{StaticResource SettingsCard}'>
                    <Grid Tag='SettingsCard'>
                      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                      <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                      <StackPanel Grid.Row='0' Grid.Column='0' Margin='0,0,16,0' VerticalAlignment='Center'>
                        <TextBlock Style='{StaticResource SettingsCardHeader}' Text='密码锁保护'/>
                        <TextBlock Style='{StaticResource SettingsCardDescription}' Text='密码锁保护界面；历史由当前 Windows 用户加密，与密码锁独立，配置和日志仍需妥善保护。自动锁定默认关闭，也可手动立即锁定。'/>
                      </StackPanel>
                      <StackPanel Grid.Row='0' Grid.Column='1' Orientation='Horizontal' HorizontalAlignment='Right' VerticalAlignment='Center'>
                        <Button x:Name='btnSecurity' Style='{StaticResource SecondaryBtn}' Content='设置密码锁'/>
                        <Button x:Name='btnDisableLock' Style='{StaticResource SecondaryBtn}' Content='关闭密码锁' Margin='8,0,0,0'/>
                        <Button x:Name='btnLock' Style='{StaticResource SecondaryBtn}' Content='立即锁定' Margin='8,0,0,0'/>
                      </StackPanel>
                    </Grid>
                  </Border>
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

          <Border x:Name='historyRecordCard' Grid.Row='0' Style='{StaticResource RecordCard}'>
            <Grid>
               <Grid.RowDefinitions>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='Auto'/>
                  <RowDefinition Height='*'/>
              </Grid.RowDefinitions>

              <StackPanel Grid.Row='0'>
                <TextBlock x:Name='historyTitle' Style='{StaticResource CardTitle}' Text='验证码记录'/>
                <Border x:Name='latestCard' Background='#E8F2FC' CornerRadius='6' Padding='12' Margin='0,0,0,8'>
                  <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                    <StackPanel>
                      <TextBlock x:Name='lblLatestCode' Text='等待验证码' FontSize='24' FontWeight='SemiBold' Foreground='#005A9E'/>
                      <TextBlock x:Name='lblLatestMeta' Style='{StaticResource Hint}' TextWrapping='Wrap' Text='收到新验证码后可在这里复制'/>
                    </StackPanel>
                    <Button x:Name='btnCopyLatest' Grid.Column='1' Style='{StaticResource PrimaryBtn}' Content='复制最新' VerticalAlignment='Center' Margin='12,0,0,0'/>
                  </Grid>
                </Border>
                <Button x:Name='btnNewRecords' Style='{StaticResource SecondaryBtn}' Content='有新记录，返回顶部' HorizontalAlignment='Left' Visibility='Collapsed' Margin='0,0,0,8'/>
              </StackPanel>

              <WrapPanel Grid.Row='1'>
                <Button x:Name='tabAllHistory' Style='{StaticResource HistoryTab}' Content='全部 (0)'/>
                <Button x:Name='tabSmsHistory' Style='{StaticResource HistoryTab}' Content='短信 (0)'/>
                <Button x:Name='tabEmailHistory' Style='{StaticResource HistoryTab}' Content='邮件 (0)'/>
              </WrapPanel>
              <StackPanel x:Name='historySearchPanel' Grid.Row='2' Margin='0,8,0,10'>
              <TextBlock x:Name='historySearchHint' Style='{StaticResource Hint}' Text='搜索记录 · Ctrl+F · 空格分隔多个词' TextWrapping='Wrap' Margin='0,0,0,6'/>
              <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                <TextBox x:Name='txtHistorySearch' Style='{StaticResource Input}' ToolTip='搜索服务名、发件人、验证码或已保存原文；多个词以空格分隔' AutomationProperties.Name='搜索验证码记录' MaxLength='256'/>
                <Button x:Name='btnHistoryReset' Grid.Column='1' Style='{StaticResource SecondaryBtn}' Content='重置筛选' Margin='8,0,0,0'/>
              </Grid>
              </StackPanel>

              <Grid x:Name='historyHeaders' Grid.Row='3' Height='30'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='112'/>
                  <ColumnDefinition Width='*'/>
                  <ColumnDefinition Width='100'/>
                  <ColumnDefinition Width='2*'/>
                  <ColumnDefinition Width='72'/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column='0' Style='{StaticResource TableHeader}' Text='时间'/>
                 <TextBlock Grid.Column='1' Style='{StaticResource TableHeader}' Text='发件人'/>
                <TextBlock Grid.Column='2' Style='{StaticResource TableHeader}' Text='验证码'/>
                 <TextBlock Grid.Column='3' Style='{StaticResource TableHeader}' Text='原文'/>
              </Grid>

              <Border Grid.Row='4' BorderBrush='#0F000000' BorderThickness='0,1,0,0'>
                <Grid>
                  <local:HistoryListBox x:Name='listHost' Margin='0,4,0,0' BorderThickness='0' Background='Transparent'
                           ScrollViewer.VerticalScrollBarVisibility='Auto' ScrollViewer.HorizontalScrollBarVisibility='Disabled'
                           ScrollViewer.CanContentScroll='True' VirtualizingPanel.IsVirtualizing='True'
                           VirtualizingPanel.VirtualizationMode='Recycling' HorizontalContentAlignment='Stretch'
                           SelectionMode='Single'>
                    <ListBox.ItemsPanel>
                      <ItemsPanelTemplate><VirtualizingStackPanel/></ItemsPanelTemplate>
                    </ListBox.ItemsPanel>
                    <ListBox.ItemContainerStyle>
                      <Style TargetType='ListBoxItem'>
                        <Setter Property='Padding' Value='0'/>
                        <Setter Property='Margin' Value='0'/>
                        <Setter Property='BorderThickness' Value='0'/>
                        <Setter Property='Background' Value='Transparent'/>
                        <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
                        <Setter Property='Focusable' Value='False'/>
                      </Style>
                    </ListBox.ItemContainerStyle>
                    <ListBox.ItemTemplate>
                      <DataTemplate>
                        <local:HistoryRowControl Owner='{Binding RelativeSource={RelativeSource AncestorType=ListBox}, Path=Tag}' Item='{Binding}'/>
                      </DataTemplate>
                    </ListBox.ItemTemplate>
                  </local:HistoryListBox>
                  <TextBlock x:Name='lblHistoryEmpty' Style='{StaticResource Hint}' HorizontalAlignment='Center'
                             VerticalAlignment='Center' Text='暂无短信验证码记录' IsHitTestVisible='False' Visibility='Collapsed'/>
                </Grid>
              </Border>
            </Grid>
          </Border>

          <Border x:Name='historyFooter' Grid.Row='1' Background='Transparent' BorderBrush='#0F000000' BorderThickness='0,1,0,0' Padding='22,12'>
            <StackPanel>
              <StackPanel x:Name='historySettings'>
              <WrapPanel Margin='0,0,0,6'>
                <TextBlock Text='自动清除超过' Style='{StaticResource Hint}' Margin='0,0,8,0'/>
                <TextBox x:Name='txtHistoryRetention' Style='{StaticResource Input}' Width='72' MaxLength='5' AutomationProperties.Name='记录自动清除分钟数'/>
                <TextBlock Text='分钟前收到的记录' Style='{StaticResource Hint}' Margin='8,0,0,0'/>
              </WrapPanel>
              <TextBlock x:Name='historyRetentionHint' Text='保留时间不代表验证码有效期；0 停用时间清理，仍最多 100 条。不删除邮箱邮件。' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,0,0,8'/>
              </StackPanel>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
              <WrapPanel Grid.Column='0' VerticalAlignment='Center'>
                <TextBlock x:Name='historyKeyboardHint' Style='{StaticResource Hint}' Text='Tab 选择操作 · Enter 执行' Margin='0,0,14,0'/>
                <TextBlock x:Name='lblCount' Style='{StaticResource Hint}' Text='共 0 条'/>
              </WrapPanel>
              <Button x:Name='btnClear' Grid.Column='1' Style='{StaticResource SecondaryBtn}' Content='清空全部记录' HorizontalAlignment='Right' VerticalAlignment='Center'/>
            </Grid>
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
                      <Button x:Name='btnUpdate' Style='{StaticResource SecondaryBtn}' Content='检查更新' Margin='8,0,0,0'/>
                     </WrapPanel>
                    <TextBlock x:Name='lblUpdateStatus' Style='{StaticResource Hint}' TextWrapping='Wrap' Margin='0,10,0,0'
                               Text='检查新版后可自动下载安装，替换程序并重启；配置和记录保留。'/>
                  </StackPanel>
                </Border>
                 <Border Style='{StaticResource Card}'>
                   <StackPanel>
                     <TextBlock Style='{StaticResource CardTitle}' Text='设置迁移'/>
                     <TextBlock Style='{StaticResource Hint}' TextWrapping='Wrap' Text='导入或导出配置文件，用于换机迁移与备份；排障请用上方的脱敏诊断。'/>
                     <WrapPanel Margin='0,14,0,0'>
                       <Button x:Name='btnImport' Style='{StaticResource SecondaryBtn}' Content='导入设置'/>
                       <Button x:Name='btnExport' Style='{StaticResource SecondaryBtn}' Content='导出设置' Margin='8,0,0,0'/>
                     </WrapPanel>
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

        <Border x:Name='settingsFeedbackHost' Grid.Row='2' Padding='18,7' BorderBrush='#0F000000' BorderThickness='0,1,0,0'>
          <TextBlock x:Name='lblSettingsFeedback' Text='就绪 · 关闭窗口后继续在托盘接收' Style='{StaticResource Hint}' TextWrapping='Wrap'/>
        </Border>

          <Border x:Name='lockOverlay' KeyboardNavigation.TabNavigation='Cycle' KeyboardNavigation.ControlTabNavigation='Cycle' Grid.Column='0' Grid.ColumnSpan='2' Grid.Row='0' Grid.RowSpan='3' Background='#F3F3F3' CornerRadius='8' Panel.ZIndex='100' Visibility='Collapsed'>
         <StackPanel Width='360' HorizontalAlignment='Center' VerticalAlignment='Center'>
           <TextBlock Text='程序已锁定' FontSize='20' FontWeight='SemiBold' Foreground='#E4000000' HorizontalAlignment='Center'/>
           <TextBlock Text='请输入密码后继续使用' Style='{StaticResource Hint}' HorizontalAlignment='Center' Margin='0,10,0,16'/>
             <Border Style='{StaticResource InputAddon}' Width='300' HorizontalAlignment='Center'>
               <Grid>
                 <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                 <Grid Grid.Column='0'>
                   <PasswordBox x:Name='txtUnlockPassword' Style='{StaticResource InnerPassword}'/>
                   <TextBox x:Name='txtUnlockPlain' Style='{StaticResource InnerInput}' Visibility='Collapsed'/>
                 </Grid>
                 <Button x:Name='btnUnlockEye' Grid.Column='1' Style='{StaticResource InlineIconBtn}' AutomationProperties.Name='显示或隐藏密码'/>
               </Grid>
             </Border>
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
