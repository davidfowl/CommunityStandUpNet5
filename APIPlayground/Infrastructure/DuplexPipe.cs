using System.IO.Pipelines;

internal class DuplexPipe : IDuplexPipe
{
    public DuplexPipe(PipeReader reader, PipeWriter writer)
    {
        Input = reader;
        Output = writer;
    }

    public PipeReader Input { get; }

    public PipeWriter Output { get; }

    public static (IDuplexPipe Transport, IDuplexPipe Application) CreatePair(PipeOptions inputOptions, PipeOptions outputOptions)
    {
        var input = new Pipe(inputOptions);
        var output = new Pipe(outputOptions);

        var transportToApplication = new DuplexPipe(output.Reader, input.Writer);
        var applicationToTransport = new DuplexPipe(input.Reader, output.Writer);

        return (applicationToTransport, transportToApplication);
    }
}