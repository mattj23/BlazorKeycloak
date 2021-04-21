using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace BlazorKeycloak.Services
{
    public sealed class TimeZoneService
    {
        private readonly IJSRuntime _jsRuntime;

        private TimeSpan? _userOffset;

        public TimeZoneService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public TimeSpan? UserOffset => _userOffset;

        public bool IsLoaded => _userOffset != null;

        public async Task LoadOffset()
        {
            if (_userOffset != null) return;
            int offsetInMinutes = await _jsRuntime.InvokeAsync<int>("blazorGetTimezoneOffset");
            _userOffset = TimeSpan.FromMinutes(-offsetInMinutes);
        }

        public async ValueTask<DateTime> GetLocalDateTime(DateTime dateTime)
        {
            if (_userOffset == null)
            {
                await LoadOffset();
            }

            return dateTime + _userOffset.Value;
        }

    }
}