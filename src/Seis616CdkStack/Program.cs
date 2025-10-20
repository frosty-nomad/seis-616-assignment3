using Amazon.CDK;

namespace Seis616CdkStack
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();

            var environment = new Amazon.CDK.Environment
            {
                Account = "703671929301",
                Region = "us-east-1"
            };

            new Seis616CdkStack(app, "Seis616CdkStack", new StackProps
            {
                Env = environment
            });
            app.Synth();
        }
    }
}
