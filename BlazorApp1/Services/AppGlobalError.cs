namespace BlazorApp1.Services;

public class AppGlobalError
{
    public event Func<Exception, Task>? OnError;

    public async Task ShowAsync(Exception exception)
    {
        if (OnError is null)
            return;

        await OnError.Invoke(exception);
    }

    public Task ShowAsync(string message)
        => ShowAsync(new Exception(message));
}
