using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Application.Services;
using Application.Interfaces.IUnitOfWork;
using Application.Interfaces.IServices;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IBackgroundServices;
using Application.Options;
using Domain.Entities;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace Capstone_Project.Tests.Services
{
    public class FileTextExtractorServiceTests
    {
        private readonly FileTextExtractorService _service;

        public FileTextExtractorServiceTests()
        {

            _service = new FileTextExtractorService(
            );
        }

        [Fact]
        public void Constructor_WhenCalled_ShouldInitialize()
        {
            _service.Should().NotBeNull();
        }
    }
}
