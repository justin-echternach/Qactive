﻿using System;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Security;
using System.Security.Permissions;
using System.Windows.Forms;
using Qactive;
using SharedLibrary;

namespace QbservableClient
{
  class MaliciousClient
  {
    public void Run()
    {
      Run(Program.SandboxedServiceEndPoint);
      Run(Program.LimitedServiceEndPoint);
    }

    private void Run(IPEndPoint serviceEndPoint)
    {
      var client = new TcpQbservableClient<int>(serviceEndPoint, typeof(MessageBox));

      // This query is allowed in both service scenarios
      IQbservable<string> query1 = client.Query()
        .Take(1)
        .Select(_ => "I'm allowed to get my permissions...\r\n" + AppDomain.CurrentDomain.PermissionSet.ToString());

      /* The sandboxed service does not prevent Console.WriteLine because that API doesn't demand any permissions.
       * This shows why setting AllowExpressionsUnrestricted to true is a bad idea, unless you trust all clients.
       */
      IQbservable<string> query2 = client.Query()
        .Take(1)
        .Do(_ => Console.WriteLine("Hello from malicious client!"))
        .Select(_ => "Malicious message sent successfully.");

      /* The default expression limiter does not prevent Environment.CurrentDirectory from being read because it's a property.
       * This shows why hosting services in a sandboxed AppDomain is a good idea, unless you trust all clients.
       */
      IQbservable<string> query3 = client.Query()
        .Take(1)
        .Select(_ => Environment.CurrentDirectory);

      // This query is prevented in both service scenarios
      IQbservable<string> query4 = client.Query()
        .Select(_ => MessageBox.Show("Hello from malicious client!").ToString());

      // This query is prevented in both service scenarios
      IQbservable<string> query5 = client.Query()
        .Select(_ => Environment.GetEnvironmentVariable("Temp", EnvironmentVariableTarget.Machine));

      // This query is prevented in both service scenarios
      IQbservable<string> query6 = client.Query()
        .Select(_ => System.IO.File.Create("c:\\malicious.exe").Length.ToString());

      // This query is prevented in both service scenarios
      IQbservable<string> query7 = client.Query()
        .Select(_ => AppDomain.CreateDomain(
          "Malicious Domain",
          null,
          new AppDomainSetup() { ApplicationBase = "C:\\Windows\\Temp\\" },
          new PermissionSet(PermissionState.Unrestricted))
          .FriendlyName);

      // This query is prevented in both service scenarios
      IQbservable<string> query8 = client.Query()
        .Select(_ => System.Reflection.Assembly.GetExecutingAssembly().CodeBase);

      // This query is prevented in both service scenarios
      IQbservable<string> query9 = client.Query()
        .Do(_ => new PermissionSet(PermissionState.Unrestricted).Assert())
        .Select(_ => string.Empty);

      Func<int, IObserver<string>> createObserver = queryNumber => Observer.Create<string>(
        value => ConsoleTrace.WriteLine(ConsoleColor.Green, "Malicious query #{0} observed: {1}", queryNumber, value),
        ex => ConsoleTrace.WriteLine(ConsoleColor.Red, "Malicious query #{0} error: {1}", queryNumber, ex.Message),
        () => ConsoleTrace.WriteLine(ConsoleColor.DarkCyan, "Malicious query #{0} completed", queryNumber));

      using (Observable.OnErrorResumeNext(
        query1.AsObservable().Do(createObserver(1)),
        query2.AsObservable().Do(createObserver(2)),
        query3.AsObservable().Do(createObserver(3)),
        query4.AsObservable().Do(createObserver(4)),
        query5.AsObservable().Do(createObserver(5)),
        query6.AsObservable().Do(createObserver(6)),
        query7.AsObservable().Do(createObserver(7)),
        query8.AsObservable().Do(createObserver(8)),
        query9.AsObservable().Do(createObserver(9)))
        .Subscribe(_ => { }, ex => { }, () => Console.WriteLine("All queries terminated. Press any key to continue...")))
      {
        Console.WriteLine();
        Console.WriteLine("Malicious client started. Waiting for query terminations...");
        Console.WriteLine();
        Console.WriteLine("(Press any key to cancel)");
        Console.WriteLine();

        Console.ReadKey(intercept: true);
      }

      IQbservable<int> safeQuery =
        from value in client.Query()
        where value > 0
        select value * 10;

      using (safeQuery.Subscribe(
        value => ConsoleTrace.WriteLine(ConsoleColor.Green, "Safe query observed: " + value),
        ex => ConsoleTrace.WriteLine(ConsoleColor.Red, "Safe query error: " + ex.Message),
        () => ConsoleTrace.WriteLine(ConsoleColor.DarkCyan, "Safe query completed")))
      {
        Console.WriteLine();
        Console.WriteLine("Waiting for service notifications...");
        Console.WriteLine();
        Console.WriteLine("(Press any key to continue)");
        Console.WriteLine();

        Console.ReadKey(intercept: true);
      }
    }
  }
}