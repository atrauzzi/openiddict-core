﻿using Microsoft.Extensions.Hosting;
using OpenIddict.Client;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Abstractions.OpenIddictExceptions;
using static OpenIddict.Client.WebIntegration.OpenIddictClientWebIntegrationConstants;

namespace OpenIddict.Sandbox.Console.Client;

using Console = System.Console;

public class InteractiveService : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly OpenIddictClientService _service;

    public InteractiveService(
        IHostApplicationLifetime lifetime,
        OpenIddictClientService service)
    {
        _lifetime = lifetime;
        _service = service;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the host to confirm that the application has started.
        var source = new TaskCompletionSource<bool>();
        using (_lifetime.ApplicationStarted.Register(static state => ((TaskCompletionSource<bool>) state!).SetResult(true), source))
        {
            await source.Task;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            string? provider;

            do
            {
                Console.WriteLine("Type '1' + ENTER to log in using the local server or '2' + ENTER to log in using GitHub.");

                provider = await WaitAsync(Task.Run(Console.ReadLine, stoppingToken), stoppingToken) switch
                {
                    "1" => "Local",
                    "2" => Providers.GitHub,
                    _   => null
                };
            }

            while (string.IsNullOrEmpty(provider));

            Console.WriteLine("Launching the system browser.");

            try
            {
                // Ask OpenIddict to initiate the authentication flow (typically, by
                // starting the system browser) and wait for the user to complete it.
                var (_, _, principal) = await _service.AuthenticateInteractivelyAsync(
                    provider, cancellationToken: stoppingToken);

                Console.WriteLine($"Welcome, {principal.FindFirst(Claims.Name)!.Value}.");
            }

            catch (OperationCanceledException)
            {
                Console.WriteLine("The authentication process was aborted.");
            }

            catch (ProtocolException exception) when (exception.Error is Errors.AccessDenied)
            {
                Console.WriteLine("The authorization was denied by the end user.");
            }

            catch
            {
                Console.WriteLine("An error occurred while trying to authenticate the user.");
            }
        }

        static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.None);

            using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>) state!).SetResult(true), source))
            {
                if (await Task.WhenAny(task, source.Task) == source.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return await task;
            }
        }
    }
}
