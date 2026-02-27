namespace Main;

public enum Shape
{
    Circle, 
    Box, 
    Triangle, 
    Cross, 
    Arc
};

public enum NoiseType
{
    MirrorFold,
    RootNoise,
    ShapeNoise,
    WarpNoise1,
    WarpNoise2,
    WaveNoise1,
    WaveNoise2
    
}

public enum FBMThreshold
{
    Min,
    Max,
    None
    
}

public enum TexOutput
{
    Output,
    ColorA,
    ColorB,
    DepthA,
    DepthB,
    Blend,
    BlendOffset,
    Noise,
    Mapping,
    Tracking
}