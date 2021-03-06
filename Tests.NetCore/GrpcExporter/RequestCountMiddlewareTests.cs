﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Tests.HttpExporter;
using static Prometheus.Tests.GrpcExporter.TestHelpers;

namespace Prometheus.Tests.GrpcExporter
{
    [TestClass]
    public class RequestCountMiddlewareTests
    {
        private Counter _counter;
        private DefaultHttpContext _httpContext;
        private RequestDelegate _requestDelegate;

        private CollectorRegistry _registry;
        private MetricFactory _factory;

        private GrpcRequestCountMiddleware _sut;

        [TestInitialize]
        public void Init()
        {
            _registry = Metrics.NewCustomRegistry();
            _factory = Metrics.WithCustomRegistry(_registry);
            _counter = _factory.CreateCounter("default_counter", "");
            _requestDelegate = context => Task.CompletedTask;

            _httpContext = new DefaultHttpContext();

            _sut = new GrpcRequestCountMiddleware(_requestDelegate, _counter);
        }

        [TestMethod]
        public void Given_null_counter_then_throws()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new GrpcRequestCountMiddleware(_requestDelegate, null));
        }

        [TestMethod]
        public void Given_invalid_labels_then_throws()
        {
            var counter = _factory.CreateCounter("invalid_labels_counter", "", "invalid");

            Assert.ThrowsException<ArgumentException>(() =>
                new GrpcRequestCountMiddleware(_requestDelegate, counter));
        }

        [TestMethod]
        public async Task Given_non_grpc_request_then_does_not_increment_counter()
        {
            Assert.AreEqual(0, _counter.Value);

            await _sut.Invoke(new DefaultHttpContext());

            Assert.AreEqual(0, _counter.Value);
        }

        [TestMethod]
        public async Task Given_request_then_increments_counter()
        {
            Assert.AreEqual(0, _counter.Value);

            SetupHttpContext(_httpContext, "CoolService", "SayHello");

            await _sut.Invoke(_httpContext);

            Assert.AreEqual(1, _counter.Value);
        }

        [TestMethod]
        public async Task Given_request_populates_labels_correctly()
        {
            var counter = _factory.CreateCounter("all_labels_counter", "", GrpcRequestLabelNames.All);

            const string expectedService = "CoolService";
            const string expectedMethod = "SayHello";
            SetupHttpContext(_httpContext, expectedService, expectedMethod);
            _sut = new GrpcRequestCountMiddleware(_requestDelegate, counter);

            await _sut.Invoke(_httpContext);

            var labels = counter.GetAllLabels().Single();
            Assert.AreEqual(
                expectedService, 
                MetricTestHelpers.GetLabelValueOrDefault(labels, GrpcRequestLabelNames.Service)
            );
            Assert.AreEqual(
                expectedMethod,
                MetricTestHelpers.GetLabelValueOrDefault(labels, GrpcRequestLabelNames.Method)
            );
        }
    }
}
