using CourierAppBackend.Abstractions;
using CourierAppBackend.Models;

namespace CourierAppBackend.Data;

public class DbUserInfoRepository : IUserInfoRepository
{
    private readonly CourierAppContext _context;
    private readonly IAddressesRepository _addressesRepository;

    public DbUserInfoRepository(CourierAppContext context, IAddressesRepository addressesRepository)
    {
        _context = context;
        _addressesRepository = addressesRepository;
    }


    public UserInfo? GetUserInfoById(string id)
    {
        return _context.UsersInfos.Find(id);
    }

    public UserInfo Add(UserInfo userInfo)
    {
        userInfo.Address = _addressesRepository.FindOrAddAddress(userInfo.Address);
        userInfo.DefaultSourceAddress = _addressesRepository.FindOrAddAddress(userInfo.DefaultSourceAddress);
        var usrInfo = (from u in _context.UsersInfos
            where u.UserId == userInfo.UserId
            select u).FirstOrDefault();
        if (usrInfo == null)
        {
            _context.UsersInfos.Add(userInfo);
        }
        else
        {
            usrInfo.Address = userInfo.Address;
            usrInfo.DefaultSourceAddress = userInfo.DefaultSourceAddress;
            usrInfo.FirstName = userInfo.FirstName;
            usrInfo.LastName = userInfo.LastName;
        }

        _context.SaveChanges();
        return userInfo;
    }
}