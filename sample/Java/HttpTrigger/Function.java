package Microsoft.Azure.WebJobs.Script.Tests.EndToEnd;

import java.util.*;
import com.microsoft.azure.functions.annotation.*;
import com.microsoft.azure.functions.*;

/**
 * Azure Functions with HTTP Trigger.
 * Create jar file for this function following instructions at:
 * https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-first-java-maven
 * groupId: Microsoft.Azure.WebJobs.Script.Tests.EndToEnd
 * artifactId: HttpTrigger
 * Accept defaults for rest of the identifiers
 * Run mvn clean package
 */
public class Function {
    @FunctionName("HttpTrigger")
    public HttpResponseMessage HttpTrigger(
            @HttpTrigger(name = "req", methods = {HttpMethod.GET, HttpMethod.POST}, authLevel = AuthorizationLevel.ANONYMOUS) HttpRequestMessage<Optional<String>> request,
            final ExecutionContext context) {
        context.getLogger().info("Java HTTP trigger processed a request.");

        // Parse query parameter
        String query = request.getQueryParameters().get("name");
        String name = request.getBody().orElse(query);

        if (name == null) {
            return request.createResponseBuilder(HttpStatus.BAD_REQUEST).body("Please pass a name on the query string or in the request body").build();
        } else {
            return request.createResponseBuilder(HttpStatus.OK).body("Hello, " + name).build();
        }
    }
}
