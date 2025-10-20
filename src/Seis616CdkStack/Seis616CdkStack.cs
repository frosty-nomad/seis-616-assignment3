using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Rds = Amazon.CDK.AWS.RDS;
using Constructs;

namespace Seis616CdkStack
{
    public class Seis616CdkStack : Stack
    {
        public Seis616CdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var vpc = new Vpc(this, "Seis616Vpc", new VpcProps
            {
                IpAddresses = IpAddresses.Cidr("10.0.0.0/16"),
                MaxAzs = 2,
                SubnetConfiguration =
                [
                    new SubnetConfiguration
                    {
                        Name = "PublicSubnet",
                        SubnetType = SubnetType.PUBLIC,
                        CidrMask = 24
                    },
                    new SubnetConfiguration
                    {
                        Name = "PrivateSubnet",
                        SubnetType = SubnetType.PRIVATE_WITH_EGRESS,
                        CidrMask = 24
                    }
                ]
            });

            // Define a Security Group for the web server to allow HTTP access.
            var webServerSecurityGroup = new SecurityGroup(this, "WebServerSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allow HTTP access to web servers",
                AllowAllOutbound = true
            });

            // Add an ingress rule to the security group for HTTP (port 80).
            webServerSecurityGroup.AddIngressRule(
                Peer.AnyIpv4(),
                Port.Tcp(80),
                "Allow HTTP access from anywhere"
            );

            // Create a user data script to install and start a basic web server.
            var userData = UserData.ForLinux();
            userData.AddCommands(
                "sudo yum update -y",
                "sudo yum install -y httpd",
                "sudo systemctl start httpd",
                "sudo systemctl enable httpd",
                "echo '<h1>Hello from CDK in an EC2 instance!</h1>' | sudo tee /var/www/html/index.html"
            );

            // Launch a web server instance in each public subnet.
            int i = 1;
            foreach (var publicSubnet in vpc.PublicSubnets)
            {
                new Instance_(this, $"WebServer{i}", new InstanceProps
                {
                    Vpc = vpc,
                    InstanceType = InstanceType.Of(InstanceClass.T2, InstanceSize.MICRO),
                    MachineImage = new AmazonLinuxImage(),
                    VpcSubnets = new SubnetSelection { Subnets = [publicSubnet] },
                    SecurityGroup = webServerSecurityGroup,
                    UserData = userData
                });
                i++;
            }

            var rdsSecurityGroup = new SecurityGroup(this, "RDSSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allow MySQL access to RDS instance",
                AllowAllOutbound = true
            });

            rdsSecurityGroup.AddIngressRule(webServerSecurityGroup, Port.Tcp(3306), "Allow MySQL traffic from web servers");

            var rdsInstance = new Rds.DatabaseInstance(this, "Seis616Database", new Rds.DatabaseInstanceProps
            {
                Engine = Rds.DatabaseInstanceEngine.Mysql(new Rds.MySqlInstanceEngineProps { Version = Rds.MysqlEngineVersion.VER_8_0_35 }),
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.MICRO),
                Vpc = vpc,
                VpcSubnets = new SubnetSelection
                {
                    SubnetType = SubnetType.PRIVATE_WITH_EGRESS
                },
                SecurityGroups = [rdsSecurityGroup],
                Credentials = Rds.Credentials.FromGeneratedSecret("admin"),
                MultiAz = true,
                DeletionProtection = false, 
                PubliclyAccessible = false
            });
        }
    }
}
