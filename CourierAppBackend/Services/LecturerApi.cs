﻿using CourierAppBackend.Configuration;
using CourierAppBackend.Models.Database;
using CourierAppBackend.Models.DTO;
using CourierAppBackend.Models.LecturerAPI;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CourierAppBackend.Services;

public class LecturerAPI(IOptions<LecturerAPIOptions> options) 
    : IApiCommunicator
{
    private readonly LecturerAPIOptions options = options.Value;
    public string Company => "Lecturer Company";

    public async Task<string> GetToken()
    {
        HttpClient client = new();
        var request = new HttpRequestMessage(HttpMethod.Post, options.tokenEndPoint)
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", options.Scope),
                new KeyValuePair<string, string>("client_id", options.client_id),
                new KeyValuePair<string, string>("client_secret", options.client_secret)
            })
        };
        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            return String.Empty;
        string responseBody = await response.Content.ReadAsStringAsync();
        var jsonResponse = JObject.Parse(responseBody);
        string accessToken = jsonResponse["access_token"]!.ToString();
        return accessToken;
    }

    public async Task<TemporaryOffer?> GetOffer(Inquiry inquiry)
    {
        CreateInquireRequestLecturer req = inquiry.ToRequest();
        var token = await GetToken();
        HttpClient client = new();
        var request = new HttpRequestMessage(HttpMethod.Post, options.apiEndPoint + "/Inquires");
        request.Headers.Add("Authorization", $"Bearer {token}");
        string jsonBody = JsonConvert.SerializeObject(req);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            return null!;
        string responseBody = await response.Content.ReadAsStringAsync();
        CreateInquireResponseLecturer apiResponse = JsonConvert.DeserializeObject<CreateInquireResponseLecturer>(responseBody)!;
        return new TemporaryOffer()
        {
            Inquiry = inquiry,
            Company = "Lecturer Company",
            InquiryID = apiResponse.InquiryId,
            TotalPrice = apiResponse.TotalPrice,
            ExpiringAt = apiResponse.ExpiringAt.ToUniversalTime(),
            Currency = apiResponse.Currency,
            PriceItems = apiResponse.PriceBreakDown.FromDTO()
        };
    }

    public async Task<TemporaryOffer?> SelectOffer(TemporaryOffer offer, CustomerInfoDTO customerInfoDTO)
    {
        CreateOfferRequestLecturer req = new()
        {
            InquiryId = offer.InquiryID,
            Name = $"{customerInfoDTO.FirstName} {customerInfoDTO.LastName}",
            Email = customerInfoDTO.Email,
            Address = new()
            {
                Street = customerInfoDTO.Address.Street,
                HouseNumber = customerInfoDTO.Address.HouseNumber,
                ApartmentNumber = customerInfoDTO.Address.ApartmentNumber,
                City = customerInfoDTO.Address.City,
                ZipCode = customerInfoDTO.Address.PostalCode
            }
        };
        var token = await GetToken();
        HttpClient client = new();
        var request = new HttpRequestMessage(HttpMethod.Post, options.apiEndPoint + "/Offers");
        request.Headers.Add("Authorization", $"Bearer {token}");
        string jsonBody = JsonConvert.SerializeObject(req);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            return null;
        string responseBody = await response.Content.ReadAsStringAsync();
        CreateOfferResponseLecturer apiResponse = JsonConvert.DeserializeObject<CreateOfferResponseLecturer>(responseBody)!;
        offer.OfferRequestId = apiResponse.OfferRequestId;
        return offer;
    }

    public async Task<OfferInfo?> GetOfferInfo(TemporaryOffer offer)
    {
        var token = await GetToken();
        HttpClient client = new();
        if (offer.OfferRequestId != string.Empty)
        {
            string offerRequestId = offer.OfferRequestId;
            var getStatusRequest = new HttpRequestMessage(HttpMethod.Get, options.apiEndPoint + $"/offer/request/{offerRequestId}/status");
            getStatusRequest.Headers.Add("Authorization", $"Bearer {token}");
            var getStatusResponse = await client.SendAsync(getStatusRequest);
            if (getStatusResponse.StatusCode != HttpStatusCode.OK)
                return null;
            string getStatusResponseBody = await getStatusResponse.Content.ReadAsStringAsync();
            CheckOfferStatusResponse checkOffer = JsonConvert.DeserializeObject<CheckOfferStatusResponse>(getStatusResponseBody)!;
            if (checkOffer.IsReady)
            {
                offer.OfferRequestId = string.Empty;
                offer.OfferID = checkOffer.OfferId!;
                var confirmRequest = new HttpRequestMessage(HttpMethod.Get, options.apiEndPoint + $"/offer/{offer.OfferID}/confirm");
                confirmRequest.Headers.Add("Authorization", $"Bearer {token}");
                var resp = await client.SendAsync(confirmRequest);
                if (resp.StatusCode != HttpStatusCode.OK)
                    return null;
            }
            else
            {
                return new() { OfferStatus = "Not ready yet" };
            }
        }
        string offerId = offer.OfferID;
        var getOfferRequest = new HttpRequestMessage(HttpMethod.Get, options.apiEndPoint + $"/offer/{offerId}");
        getOfferRequest.Headers.Add("Authorization", $"Bearer {token}");
        var getOfferResponse = await client.SendAsync(getOfferRequest);
        if (getOfferResponse.StatusCode != HttpStatusCode.OK)
            return null;
        string getOfferResponseBody = await getOfferResponse.Content.ReadAsStringAsync();
        GetOfferResponseLecturer getOffer = JsonConvert.DeserializeObject<GetOfferResponseLecturer>(getOfferResponseBody)!;
        return getOffer.ToDTO();
    }
}
