namespace Main;

public enum ComfyJobStatus
{
    CREATED,
    QUEUED,
    PROCESSING,
    DONE,
    TIMEOUT,
    CANCELED,
    ERROR,
	INVALID,
	JOBLESS
}

public enum Room
{
    ALL,
    DGN,
    ENT,
    GAS,
    HOF
}
public enum ComfyMode
{
    GENERATE,
    UPSCALE
}
