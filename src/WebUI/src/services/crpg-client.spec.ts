import { mockGet, mockPost, mockPut, mockDelete } from 'vi-fetch';
import { get, post, put, del, JSONDateToJs } from './crpg-client';
import { ErrorType, type Result } from '@/models/crpg-client-result';

const { mockedGetToken, mockedLogin, mockedNotify, mockedSleep } = vi.hoisted(() => ({
  mockedGetToken: vi.fn(),
  mockedLogin: vi.fn(),
  mockedNotify: vi.fn(),
  mockedSleep: vi.fn().mockResolvedValue(null),
}));

vi.mock('@/services/auth-service', () => ({
  getToken: mockedGetToken,
  login: mockedLogin,
}));

vi.mock('@/utils/promise', () => ({
  sleep: mockedSleep,
}));

vi.mock('@/services/notification-service', async () => {
  return {
    ...(await vi.importActual<typeof import('@/services/notification-service')>(
      '@/services/notification-service'
    )),
    notify: mockedNotify,
  };
});

describe('get', () => {
  const path = '/test-get';

  it('OK 200', async () => {
    const response: Result<any> = {
      data: {
        name: 'Rainbow Dash',
        description: 'The best',
      },
      errors: null,
    };

    mockGet(path).willResolve(response, 200);

    const result = await get(path);

    expect(result).toEqual(response.data);
  });

  it('with error - InternalError', async () => {
    const response: Result<any> = {
      data: null,
      errors: [
        {
          traceId: null,
          type: ErrorType.InternalError,
          code: '500',
          title: 'some error',
          detail: null,
          stackTrace: null,
        },
      ],
    };

    mockGet(path).willResolve(response, 200);

    // ref https://vitest.dev/api/#rejects
    await expect(get(path)).rejects.toThrow('Server error');

    expect(mockedNotify).toBeCalledWith('some error', 'danger');
  });

  it('with error - other', async () => {
    const response: Result<any> = {
      data: null,
      errors: [
        {
          traceId: null,
          type: ErrorType.Forbidden,
          code: '500',
          title: 'some warning',
          detail: null,
          stackTrace: null,
        },
      ],
    };

    mockGet(path).willResolve(response, 200);

    await expect(get(path)).rejects.toThrow('Bad request');

    expect(mockedNotify).toBeCalledWith('some warning', 'warning');
  });

  it('Unauthorized', async () => {
    mockGet(path).willFail({}, 401);

    const result = await get(path);
    expect(result).toEqual(null);

    expect(mockedNotify).toBeCalledWith('Session expired', 'warning');
    expect(mockedSleep).toBeCalled();
  });
});

describe('post', () => {
  const path = '/test-post';

  it('OK 204 NO_CONTENT', async () => {
    const response: Result<any> = {
      data: {},
      errors: null,
    };

    mockPost(path).willResolve(response, 204);

    const result = await post(path, {
      name: 'Rainbow Dash',
      description: 'The best',
    });

    expect(result).toEqual(null);
  });
});

describe('put', () => {
  const path = '/test-put/1';

  it('OK 204', async () => {
    const response: Result<any> = {
      data: {},
      errors: null,
    };

    mockPut(path).willResolve(response, 204);

    const result = await put(path, {
      name: 'Rainbow Dash',
      description: 'The best',
    });

    expect(result).toEqual(null);
  });

  describe('del', () => {
    const path = '/test-del/1';

    it('OK 204', async () => {
      const response: Result<any> = {
        data: {},
        errors: null,
      };

      mockDelete(path).willResolve(response, 204);

      const result = await del(path);

      expect(result).toEqual(null);
    });
  });
});

it('JSONDateToJs', () => {
  expect(JSONDateToJs('2023-11-17T18:50:13.659473Z')).toEqual(new Date('2023-11-17T18:50:13.659Z'));

  expect(
    JSONDateToJs({
      createdAt: '2023-11-17T18:50:13.659473Z',
    })
  ).toEqual({ createdAt: new Date('2023-11-17T18:50:13.659Z') });

  expect(
    JSONDateToJs({
      id: 1,
      nested: {
        createdAt: '2023-11-17T18:50:13.659473Z',
      },
    })
  ).toEqual({
    id: 1,
    nested: {
      createdAt: new Date('2023-11-17T18:50:13.659473Z'),
    },
  });
});
