hostasp
=======

a solution for self-hosting ASP.NET MVC


It’s super easy to setup, from the hosting project, include references to this solution (hostasp) and the main class in your web project (some HttpApplication derived class) and call:

AppHost.HostAndSetup<in T>(string virtualDir, string relativePath, int port) where T : HttpApplication

and you’ll have your webproject serving requests when it returns.
