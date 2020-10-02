using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Teams.Apps.FAQPlusPlus.Bots;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration;
using Microsoft.Teams.Apps.FAQPlusPlus.Common.Providers;
using Microsoft.Teams.Apps.FAQPlusPlus.Controllers;
using Moq;
using Xunit;

namespace Microsoft.Teams.Apps.FAQPlusPlus.Tests.Controllers
{
    public class BotControllerTests
    {
        [Fact]
        public async Task PostAsyncCallsProcessAsyncOnAdapter()
        {
            // Create MVC infrastructure mocks and objects
            var request = new Mock<HttpRequest>();
            var response = new Mock<HttpResponse>();
            var mockHttpContext = new Mock<HttpContext>();
            mockHttpContext.Setup(x => x.Request).Returns(request.Object);
            mockHttpContext.Setup(x => x.Response).Returns(response.Object);
            var actionContext = new ActionContext(mockHttpContext.Object, new RouteData(), new ControllerActionDescriptor());

            // Create BF mocks
            var mockAdapter = new Mock<IBotFrameworkHttpAdapter>();
            mockAdapter
                .Setup(x => x.ProcessAsync(It.IsAny<HttpRequest>(), It.IsAny<HttpResponse>(), It.IsAny<IBot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var (mockUserBot, mockSmeBot) = GetBotMockInstances();

            // Create and initialize controller
            var sut = new BotController(mockAdapter.Object, mockUserBot, mockSmeBot)
            {
                ControllerContext = new ControllerContext(actionContext),
            };

            // Invoke the controller
            await sut.PostUserAsync();

            // Assert
            mockAdapter.Verify(
                x => x.ProcessAsync(
                    It.Is<HttpRequest>(o => o == request.Object),
                    It.Is<HttpResponse>(o => o == response.Object),
                    It.Is<IBot>(o => o == mockUserBot),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static (UserActivityHandler mockUserBot, SmeActivityHandler mockSmeBot) GetBotMockInstances()
        {
            var mockConfigProvider = new Mock<IConfigurationDataProvider>();
            var botSettings = new BotSettings();
            var mockBotSettingsMonitor = Mock.Of<IOptionsMonitor<BotSettings>>(_ => _.CurrentValue == botSettings);
            var mockUserBotLogger = new Mock<ILogger<UserActivityHandler>>();
            var mockSmeBotLogger = new Mock<ILogger<SmeActivityHandler>>();
            var appCredentialsProvider = new MicrosoftAppCredentials("", "");
            var ticketProvider = new Mock<ITicketsProvider>();
            var qnaService = new Mock<IQnaServiceProvider>();

            var mockUserBot = new UserActivityHandler(mockConfigProvider.Object, appCredentialsProvider,
                ticketProvider.Object, qnaService.Object, mockBotSettingsMonitor, mockUserBotLogger.Object);

            var mockSmeBot = new SmeActivityHandler(mockConfigProvider.Object, appCredentialsProvider,
                ticketProvider.Object, qnaService.Object, new Mock<IActivityStorageProvider>().Object,
                new Mock<ISearchService>().Object, null, new MemoryCache(new MemoryCacheOptions()),
                new Mock<IKnowledgeBaseSearchService>().Object, mockBotSettingsMonitor, mockSmeBotLogger.Object);
            return (mockUserBot, mockSmeBot);
        }
    }
}
